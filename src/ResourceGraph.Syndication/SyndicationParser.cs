using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ResourceGraph.Core;

namespace ResourceGraph.Syndication;

/// <summary>Parses RSS 2.0 and Atom documents without performing network requests.</summary>
public static class SyndicationParser
{
    private static readonly XNamespace ItunesNamespace = "http://www.itunes.com/dtds/podcast-1.0.dtd";
    private static readonly XNamespace MediaNamespace = "http://search.yahoo.com/mrss/";
    private static readonly XNamespace YouTubeNamespace = "http://www.youtube.com/xml/schemas/2015";

    /// <summary>Parses a UTF-8 or Unicode XML string.</summary>
    public static SyndicationFeed Parse(string xml, FeedParseOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new ArgumentException("XML content is required.", nameof(xml));
        }

        var effectiveOptions = options ?? FeedParseOptions.Default;
        var byteCount = Encoding.UTF8.GetByteCount(xml);
        if (byteCount > effectiveOptions.MaxBytes)
        {
            throw new SyndicationLimitExceededException(
                $"The syndication document is {byteCount} bytes; the limit is {effectiveOptions.MaxBytes}.");
        }

        return ParseDocument(xml, effectiveOptions);
    }

    /// <summary>Parses a bounded stream without closing the caller-owned stream.</summary>
    public static SyndicationFeed Parse(Stream stream, FeedParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        var effectiveOptions = options ?? FeedParseOptions.Default;
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        var total = 0;
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            total += read;
            if (total > effectiveOptions.MaxBytes)
            {
                throw new SyndicationLimitExceededException(
                    $"The syndication stream exceeds the {effectiveOptions.MaxBytes}-byte limit.");
            }

            buffer.Write(chunk, 0, read);
        }

        return ParseDocument(Encoding.UTF8.GetString(buffer.ToArray()), effectiveOptions);
    }

    /// <summary>Parses a local file without making a network request.</summary>
    public static SyndicationFeed ParseFile(string path, FeedParseOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A file path is required.", nameof(path));
        }

        using var stream = File.OpenRead(path);
        return Parse(stream, options);
    }

    private static SyndicationFeed ParseDocument(string xml, FeedParseOptions options)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = options.MaxBytes,
                MaxCharactersFromEntities = 0,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            };

            using var stringReader = new StringReader(xml);
            using var reader = XmlReader.Create(stringReader, settings);
            var document = XDocument.Load(reader, LoadOptions.None);
            if (document.Root is null)
            {
                throw new SyndicationParseException("The XML document has no root element.");
            }

            EnsureDepth(document.Root, 1, options.MaxElementDepth);
            return document.Root.Name.LocalName.ToLowerInvariant() switch
            {
                "rss" => ParseRss(document.Root, options),
                "feed" => ParseAtom(document.Root, options),
                _ => throw new SyndicationParseException(
                    $"Unsupported syndication root '{document.Root.Name.LocalName}'.")
            };
        }
        catch (SyndicationParseException)
        {
            throw;
        }
        catch (XmlException exception)
        {
            throw new SyndicationParseException("The syndication XML is malformed.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new SyndicationParseException("The syndication document is invalid.", exception);
        }
    }

    private static SyndicationFeed ParseRss(XElement root, FeedParseOptions options)
    {
        var channel = Child(root, "channel")
            ?? throw new SyndicationParseException("RSS is missing its channel element.");
        var diagnostics = ImmutableArray.CreateBuilder<GraphDiagnostic>();
        var itemElements = Children(channel, "item").ToArray();
        EnsureItemLimit(itemElements.Length, options);

        var items = itemElements
            .Select((item, index) => ReadRssItem(item, index, diagnostics))
            .ToArray();
        return new SyndicationFeed(
            SyndicationFormat.Rss,
            Text(channel, "id") ?? Text(channel, "guid"),
            Text(channel, "title"),
            ReadUri(Text(channel, "link"), "feed.link", diagnostics),
            Text(channel, "description"),
            items,
            diagnostics,
            ReadMetadata(channel));
    }

    private static SyndicationFeed ParseAtom(XElement root, FeedParseOptions options)
    {
        var diagnostics = ImmutableArray.CreateBuilder<GraphDiagnostic>();
        var entryElements = Children(root, "entry").ToArray();
        EnsureItemLimit(entryElements.Length, options);

        var items = entryElements
            .Select((entry, index) => ReadAtomItem(entry, index, diagnostics))
            .ToArray();
        return new SyndicationFeed(
            SyndicationFormat.Atom,
            Text(root, "id"),
            Text(root, "title"),
            ReadAtomLink(root, diagnostics),
            Text(root, "subtitle"),
            items,
            diagnostics,
            ReadMetadata(root));
    }

    private static SyndicationItem ReadRssItem(
        XElement item,
        int index,
        ImmutableArray<GraphDiagnostic>.Builder diagnostics)
    {
        var enclosures = ImmutableArray.CreateBuilder<SyndicationEnclosure>();
        foreach (var element in item.Elements())
        {
            var localName = element.Name.LocalName;
            if (localName.Equals("enclosure", StringComparison.OrdinalIgnoreCase) ||
                (element.Name.Namespace == MediaNamespace &&
                 localName.Equals("content", StringComparison.OrdinalIgnoreCase)) ||
                (element.Name.Namespace == MediaNamespace &&
                 localName.Equals("thumbnail", StringComparison.OrdinalIgnoreCase)))
            {
                var uri = ReadUri(
                    (string?)element.Attribute("url") ?? (string?)element.Attribute("href"),
                    $"item[{index}].{localName}",
                    diagnostics);
                if (uri is null)
                {
                    continue;
                }

                enclosures.Add(new SyndicationEnclosure(
                    uri,
                    (string?)element.Attribute("type"),
                    ParseLength((string?)element.Attribute("length") ?? (string?)element.Attribute("fileSize")),
                    (string?)element.Attribute("duration") ?? Text(item, "duration", ItunesNamespace),
                    element.Name.Namespace == MediaNamespace ? $"media:{localName}" : "enclosure"));
            }
        }

        var link = ReadUri(Text(item, "link"), $"item[{index}].link", diagnostics);
        var id = Text(item, "guid") ?? link?.AbsoluteUri;
        var published = ReadDate(
            Text(item, "pubDate") ?? Text(item, "date"),
            $"item[{index}].published",
            diagnostics);
        var updated = ReadDate(Text(item, "updated"), $"item[{index}].updated", diagnostics);

        return new SyndicationItem(
            id,
            Text(item, "title"),
            link,
            Text(item, "description") ?? Text(item, "content", "http://purl.org/rss/1.0/modules/content/"),
            published,
            updated,
            enclosures,
            ReadMetadata(item));
    }

    private static SyndicationItem ReadAtomItem(
        XElement entry,
        int index,
        ImmutableArray<GraphDiagnostic>.Builder diagnostics)
    {
        var enclosures = ImmutableArray.CreateBuilder<SyndicationEnclosure>();
        foreach (var link in Children(entry, "link"))
        {
            var rel = (string?)link.Attribute("rel");
            if (!string.Equals(rel, "enclosure", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var uri = ReadUri((string?)link.Attribute("href"), $"entry[{index}].enclosure", diagnostics);
            if (uri is not null)
            {
                enclosures.Add(new SyndicationEnclosure(
                    uri,
                    (string?)link.Attribute("type"),
                    ParseLength((string?)link.Attribute("length")),
                    null,
                    "enclosure"));
            }
        }

        var linkUri = ReadAtomLink(entry, diagnostics);
        return new SyndicationItem(
            Text(entry, "id") ?? linkUri?.AbsoluteUri,
            Text(entry, "title"),
            linkUri,
            Text(entry, "summary") ?? Text(entry, "content"),
            ReadDate(
                Text(entry, "published"),
                $"entry[{index}].published",
                diagnostics),
            ReadDate(
                Text(entry, "updated"),
                $"entry[{index}].updated",
                diagnostics),
            enclosures,
            ReadMetadata(entry));
    }

    private static Uri? ReadAtomLink(
        XElement parent,
        ImmutableArray<GraphDiagnostic>.Builder diagnostics)
    {
        var link = Children(parent, "link")
            .FirstOrDefault(candidate =>
                string.IsNullOrWhiteSpace((string?)candidate.Attribute("rel")) ||
                string.Equals((string?)candidate.Attribute("rel"), "alternate", StringComparison.OrdinalIgnoreCase));
        return ReadUri((string?)link?.Attribute("href"), "atom.link", diagnostics);
    }

    private static ImmutableDictionary<string, string> ReadMetadata(XElement parent)
    {
        var metadata = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var element in parent.Elements().Where(element => !string.IsNullOrEmpty(element.Name.NamespaceName)))
        {
            var value = element.Value.Trim();
            if (value.Length > 0)
            {
                metadata[$"{element.Name.NamespaceName}:{element.Name.LocalName}"] = value;
            }

            foreach (var attribute in element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration))
            {
                var attributeValue = attribute.Value.Trim();
                if (attributeValue.Length > 0)
                {
                    metadata[
                        $"{element.Name.NamespaceName}:{element.Name.LocalName}@{attribute.Name.LocalName}"] =
                        attributeValue;
                }
            }
        }

        return metadata.ToImmutable();
    }

    private static DateTimeOffset? ReadDate(
        string? value,
        string field,
        ImmutableArray<GraphDiagnostic>.Builder diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            return parsed;
        }

        diagnostics.Add(new GraphDiagnostic(
            "syndication.date.invalid",
            $"The value '{value}' is not a valid date for {field}.",
            DiagnosticSeverity.Warning));
        return null;
    }

    private static Uri? ReadUri(
        string? value,
        string field,
        ImmutableArray<GraphDiagnostic>.Builder diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return uri;
        }

        diagnostics.Add(new GraphDiagnostic(
            "syndication.uri.invalid",
            $"The value '{value}' is not an absolute URI for {field}.",
            DiagnosticSeverity.Warning));
        return null;
    }

    private static long? ParseLength(string? value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && result >= 0
            ? result
            : null;

    private static string? Text(XElement parent, string localName, XNamespace? ns = null) =>
        parent.Elements()
            .FirstOrDefault(element =>
                element.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase) &&
                (ns is null || element.Name.Namespace == ns))
            ?.Value
            .Trim();

    private static XElement? Child(XElement parent, string localName) =>
        parent.Elements()
            .FirstOrDefault(element =>
                element.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<XElement> Children(XElement parent, string localName) =>
        parent.Elements()
            .Where(element => element.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));

    private static void EnsureItemLimit(int count, FeedParseOptions options)
    {
        if (count > options.MaxItems)
        {
            throw new SyndicationLimitExceededException(
                $"The feed contains {count} items; the limit is {options.MaxItems}.");
        }
    }

    private static void EnsureDepth(XElement element, int depth, int maxDepth)
    {
        if (depth > maxDepth)
        {
            throw new SyndicationLimitExceededException(
                $"The XML element depth exceeds the limit of {maxDepth}.");
        }

        foreach (var child in element.Elements())
        {
            EnsureDepth(child, depth + 1, maxDepth);
        }
    }
}
