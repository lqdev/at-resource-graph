using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using ResourceGraph.Core;

namespace ResourceGraph.Discovery;

/// <summary>Parses only link and AT metadata inside an HTML head.</summary>
public static partial class HtmlHeadParser
{
    private static readonly Regex TagRegex = BuildTagRegex();
    private static readonly Regex AttributeRegex = BuildAttributeRegex();
    private static readonly Regex ScriptRegex = BuildScriptRegex();
    private static readonly char[] RelSeparators = [' ', '\t', '\r', '\n', ','];

    /// <summary>Parses a UTF-8 string without reading or interpreting the page body.</summary>
    public static DiscoveryResult Parse(string html, DiscoveryPolicy? policy = null)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new ArgumentException("HTML content is required.", nameof(html));
        }

        var effectivePolicy = policy ?? DiscoveryPolicy.Default;
        var sourceBytes = Encoding.UTF8.GetBytes(html);
        var cappedBytes = sourceBytes.Length > effectivePolicy.MaxHeadBytes
            ? sourceBytes[..effectivePolicy.MaxHeadBytes]
            : sourceBytes;
        var capped = Encoding.UTF8.GetString(cappedBytes);
        var headStart = IndexOfIgnoreCase(capped, "<head");
        if (headStart < 0)
        {
            return new DiscoveryResult(
                Array.Empty<DiscoveredLink>(),
                new[]
                {
                    new GraphDiagnostic(
                        "discovery.head.missing",
                        "No HTML head element was found.",
                        DiagnosticSeverity.Warning)
                },
                cappedBytes.Length,
                false);
        }

        var headOpenEnd = capped.IndexOf('>', headStart);
        if (headOpenEnd < 0)
        {
            return new DiscoveryResult(
                Array.Empty<DiscoveredLink>(),
                new[]
                {
                    new GraphDiagnostic(
                        "discovery.head.unterminated",
                        "The HTML head start tag is incomplete.",
                        DiagnosticSeverity.Warning)
                },
                cappedBytes.Length,
                false);
        }

        var headClose = IndexOfIgnoreCase(capped, "</head", headOpenEnd + 1);
        var reachedHeadEnd = headClose >= 0;
        var headEnd = reachedHeadEnd ? headClose : capped.Length;
        var head = capped[(headOpenEnd + 1)..headEnd];
        var diagnostics = ImmutableArray.CreateBuilder<GraphDiagnostic>();
        if (!reachedHeadEnd && sourceBytes.Length <= effectivePolicy.MaxHeadBytes)
        {
            diagnostics.Add(new GraphDiagnostic(
                "discovery.head.unterminated",
                "The source ended before a closing head tag.",
                DiagnosticSeverity.Warning));
        }

        head = ScriptRegex.Replace(head, string.Empty);
        var links = ImmutableArray.CreateBuilder<DiscoveredLink>();
        foreach (Match tag in TagRegex.Matches(head))
        {
            var attributes = ParseAttributes(tag.Groups["attrs"].Value);
            var tagName = tag.Groups["tag"].Value.ToLowerInvariant();
            if (tagName == "link")
            {
                ParseLink(attributes, links, diagnostics, effectivePolicy);
            }
            else if (tagName == "meta")
            {
                ParseMeta(attributes, links, diagnostics, effectivePolicy);
            }
        }

        if (sourceBytes.Length > effectivePolicy.MaxHeadBytes)
        {
            diagnostics.Add(new GraphDiagnostic(
                "discovery.head.limit",
                $"Head scanning stopped at {effectivePolicy.MaxHeadBytes} UTF-8 bytes.",
                DiagnosticSeverity.Warning));
        }

        return new DiscoveryResult(links, diagnostics, cappedBytes.Length, reachedHeadEnd);
    }

    /// <summary>Parses a bounded stream without closing it.</summary>
    public static DiscoveryResult Parse(Stream stream, DiscoveryPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        var effectivePolicy = policy ?? DiscoveryPolicy.Default;
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        var total = 0;
        int read;
        while (total <= effectivePolicy.MaxHeadBytes &&
               (read = stream.Read(chunk, 0, Math.Min(chunk.Length, effectivePolicy.MaxHeadBytes + 1 - total))) > 0)
        {
            buffer.Write(chunk, 0, read);
            total += read;
        }

        return Parse(Encoding.UTF8.GetString(buffer.ToArray()), effectivePolicy);
    }

    /// <summary>Parses a local HTML file without making a network request.</summary>
    public static DiscoveryResult ParseFile(string path, DiscoveryPolicy? policy = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A file path is required.", nameof(path));
        }

        using var stream = File.OpenRead(path);
        return Parse(stream, policy);
    }

    private static void ParseLink(
        Dictionary<string, string> attributes,
        ImmutableArray<DiscoveredLink>.Builder links,
        ImmutableArray<GraphDiagnostic>.Builder diagnostics,
        DiscoveryPolicy policy)
    {
        if (!attributes.TryGetValue("href", out var href))
        {
            return;
        }

        if (!ResourceUri.TryCreate(href, out var uri))
        {
            diagnostics.Add(new GraphDiagnostic(
                "discovery.uri.invalid",
                $"The metadata href '{href}' is not an absolute URI.",
                DiagnosticSeverity.Warning));
            return;
        }

        var rel = attributes.GetValueOrDefault("rel");
        var type = attributes.GetValueOrDefault("type")?.ToLowerInvariant();
        var relTokens = (rel ?? string.Empty)
            .Split(RelSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        var kind = type switch
        {
            "application/rss+xml" when relTokens.Contains("alternate") => DiscoveryLinkKind.Rss,
            "application/atom+xml" when relTokens.Contains("alternate") => DiscoveryLinkKind.Atom,
            _ when type == "text/x-opml" || relTokens.Contains("opml") || relTokens.Contains("outline") =>
                DiscoveryLinkKind.Opml,
            _ when relTokens.Contains("site.standard.document") => DiscoveryLinkKind.StandardSiteVerification,
            _ => (DiscoveryLinkKind?)null
        };

        if (kind is null)
        {
            return;
        }

        AddIfAllowed(
            kind.Value,
            uri,
            rel,
            type,
            attributes.GetValueOrDefault("title"),
            kind == DiscoveryLinkKind.StandardSiteVerification,
            href,
            links,
            diagnostics,
            policy);
    }

    private static void ParseMeta(
        Dictionary<string, string> attributes,
        ImmutableArray<DiscoveredLink>.Builder links,
        ImmutableArray<GraphDiagnostic>.Builder diagnostics,
        DiscoveryPolicy policy)
    {
        if (!attributes.TryGetValue("name", out var name) ||
            !name.StartsWith("at:", StringComparison.OrdinalIgnoreCase) ||
            !attributes.TryGetValue("content", out var content))
        {
            return;
        }

        if (!ResourceUri.TryCreate(content, out var uri))
        {
            diagnostics.Add(new GraphDiagnostic(
                "discovery.uri.invalid",
                $"The AT metadata value '{content}' is not an absolute URI.",
                DiagnosticSeverity.Warning));
            return;
        }

        var kind = name.ToLowerInvariant() switch
        {
            "at:canonical" => DiscoveryLinkKind.AtCanonical,
            "at:alternate" => DiscoveryLinkKind.AtAlternate,
            "at:author" => DiscoveryLinkKind.AtAuthor,
            "at:me" => DiscoveryLinkKind.AtMe,
            _ => (DiscoveryLinkKind?)null
        };
        if (kind is null)
        {
            return;
        }

        AddIfAllowed(
            kind.Value,
            uri,
            name,
            "text/at-uri",
            null,
            allowAtUri: true,
            content,
            links,
            diagnostics,
            policy);
    }

    private static void AddIfAllowed(
        DiscoveryLinkKind kind,
        Uri uri,
        string? rel,
        string? mediaType,
        string? title,
        bool allowAtUri,
        string uriText,
        ImmutableArray<DiscoveredLink>.Builder links,
        ImmutableArray<GraphDiagnostic>.Builder diagnostics,
        DiscoveryPolicy policy)
    {
        if (!policy.IsAllowed(uri, allowAtUri))
        {
            diagnostics.Add(new GraphDiagnostic(
                "discovery.uri.rejected",
                $"The discovered URI '{uri}' was rejected by the discovery policy.",
                DiagnosticSeverity.Warning,
                uri.AbsoluteUri));
            return;
        }

        links.Add(new DiscoveredLink(kind, uri, rel, mediaType, title, uriText));
    }

    private static Dictionary<string, string> ParseAttributes(string source)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex.Matches(source))
        {
            var name = match.Groups["name"].Value.ToLowerInvariant();
            var value = match.Groups["dq"].Success
                ? match.Groups["dq"].Value
                : match.Groups["sq"].Success
                    ? match.Groups["sq"].Value
                    : match.Groups["bare"].Value;
            attributes.TryAdd(name, value);
        }

        return attributes;
    }

    private static int IndexOfIgnoreCase(string source, string value, int startIndex = 0) =>
        source.IndexOf(value, startIndex, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex("<(?<tag>link|meta)\\b(?<attrs>[^>]*?)/?>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex BuildTagRegex();

    [GeneratedRegex("(?<name>[A-Za-z_:][A-Za-z0-9_:.-]*)\\s*=\\s*(?:\"(?<dq>[^\"]*)\"|'(?<sq>[^']*)'|(?<bare>[^\\s>]+))", RegexOptions.Singleline)]
    private static partial Regex BuildAttributeRegex();

    [GeneratedRegex("<(script|style)\\b[^>]*>[\\s\\S]*?</\\1\\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BuildScriptRegex();
}
