using System.Text;
using System.Xml;
using System.Xml.Linq;
using ResourceGraph.Core;

namespace ResourceGraph.Opml;

/// <summary>Imports conventional and graph OPML documents without network access.</summary>
public static class OpmlImporter
{
    /// <summary>Imports a UTF-8 OPML string.</summary>
    public static OpmlImportResult Import(
        string xml,
        OpmlProfile profile = OpmlProfile.Conventional,
        OpmlImportOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new ArgumentException("OPML XML is required.", nameof(xml));
        }

        var effectiveOptions = options ?? OpmlImportOptions.Default;
        var bytes = Encoding.UTF8.GetByteCount(xml);
        if (bytes > effectiveOptions.MaxBytes)
        {
            throw new InvalidDataException(
                $"The OPML document is {bytes} bytes; the limit is {effectiveOptions.MaxBytes}.");
        }

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = effectiveOptions.MaxBytes
            };
            using var stringReader = new StringReader(xml);
            using var reader = XmlReader.Create(stringReader, settings);
            var document = XDocument.Load(reader, LoadOptions.None);
            return ReadDocument(document, profile, effectiveOptions);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException("The OPML XML is malformed.", exception);
        }
    }

    /// <summary>Imports a local OPML file without making network requests.</summary>
    public static OpmlImportResult ImportFile(
        string path,
        OpmlProfile profile = OpmlProfile.Conventional,
        OpmlImportOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A file path is required.", nameof(path));
        }

        return Import(File.ReadAllText(path), profile, options);
    }

    private static OpmlImportResult ReadDocument(
        XDocument document,
        OpmlProfile profile,
        OpmlImportOptions options)
    {
        var body = document.Root?.Element("body")
            ?? throw new InvalidDataException("OPML is missing its body element.");
        var outlineElements = body.Descendants("outline").ToArray();
        if (outlineElements.Length > options.MaxOutlines)
        {
            throw new InvalidDataException(
                $"The OPML document contains {outlineElements.Length} outlines; the limit is {options.MaxOutlines}.");
        }

        var diagnostics = new List<GraphDiagnostic>();
        var members = new List<Membership>();
        var index = 0;
        foreach (var outline in outlineElements)
        {
            var reference = ReadReference(outline, profile, diagnostics);
            if (reference is null)
            {
                continue;
            }

            var position = ReadPosition(outline, profile) ?? index;
            var label = ReadLabel(outline, profile);
            try
            {
                members.Add(new Membership(position, reference, label));
                index++;
            }
            catch (ArgumentException exception)
            {
                diagnostics.Add(new GraphDiagnostic(
                    "opml.membership.invalid",
                    exception.Message,
                    DiagnosticSeverity.Error,
                    reference.Identity,
                    position));
            }
        }

        var title = document.Root?.Element("head")?.Element("title")?.Value;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = "Imported OPML";
        }

        try
        {
            return new OpmlImportResult(new Bundle(title.Trim(), members), diagnostics);
        }
        catch (ArgumentException exception)
        {
            diagnostics.Add(new GraphDiagnostic(
                "opml.bundle.invalid",
                exception.Message,
                DiagnosticSeverity.Error));
            return new OpmlImportResult(null, diagnostics);
        }
    }

    private static ResourceReference? ReadReference(
        XElement outline,
        OpmlProfile profile,
        List<GraphDiagnostic> diagnostics)
    {
        var graphNamespace = XNamespace.Get(OpmlExporter.GraphNamespace);
        var kind = profile == OpmlProfile.Graph
            ? (string?)outline.Attribute(graphNamespace + "kind")
            : null;
        var uriValue = profile == OpmlProfile.Graph
            ? (string?)outline.Attribute(graphNamespace + "uri")
            : null;
        var feedUri = (string?)outline.Attribute("xmlUrl");
        var linkUri = uriValue ?? (string?)outline.Attribute("url") ?? feedUri;
        if (!ResourceUri.TryCreate(linkUri, out var uri))
        {
            diagnostics.Add(new GraphDiagnostic(
                "opml.uri.invalid",
                $"Outline '{(string?)outline.Attribute("text") ?? "(untitled)"}' has no absolute URI.",
                DiagnosticSeverity.Error));
            return null;
        }

        try
        {
            if (string.Equals(kind, "at-record", StringComparison.OrdinalIgnoreCase) ||
                (kind is null && uri.Scheme.Equals("at", StringComparison.OrdinalIgnoreCase)))
            {
                return new AtRecordReference(
                    linkUri!,
                    (string?)outline.Attribute(graphNamespace + "cid"));
            }

            if (string.Equals(kind, "nested-bundle", StringComparison.OrdinalIgnoreCase))
            {
                var mode = string.Equals(
                    (string?)outline.Attribute(graphNamespace + "mode"),
                    "frozen",
                    StringComparison.OrdinalIgnoreCase)
                    ? BundleReferenceMode.Frozen
                    : BundleReferenceMode.Live;
                return new NestedBundleReference(linkUri!, mode);
            }

            if (string.Equals(kind, "discovery", StringComparison.OrdinalIgnoreCase) ||
                (kind is null && string.Equals((string?)outline.Attribute("type"), "link", StringComparison.OrdinalIgnoreCase)))
            {
                return new ExplicitDiscoveryReference(uri);
            }

            if (string.Equals(kind, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                return new UnknownResourceReference(
                    uri.AbsoluteUri,
                    (string?)outline.Attribute(graphNamespace + "type") ?? "opml.unknown");
            }

            var format = string.Equals(
                (string?)outline.Attribute(graphNamespace + "format"),
                "atom",
                StringComparison.OrdinalIgnoreCase)
                ? SyndicationFormat.Atom
                : SyndicationFormat.Rss;
            return new SyndicationReference(uri, format);
        }
        catch (ArgumentException exception)
        {
            diagnostics.Add(new GraphDiagnostic(
                "opml.reference.invalid",
                exception.Message,
                DiagnosticSeverity.Error,
                uri.AbsoluteUri));
            return null;
        }
    }

    private static int? ReadPosition(XElement outline, OpmlProfile profile)
    {
        if (profile != OpmlProfile.Graph)
        {
            return null;
        }

        var graphNamespace = XNamespace.Get(OpmlExporter.GraphNamespace);
        return int.TryParse((string?)outline.Attribute(graphNamespace + "position"), out var value)
            ? value
            : null;
    }

    private static string? ReadLabel(XElement outline, OpmlProfile profile)
    {
        var graphNamespace = XNamespace.Get(OpmlExporter.GraphNamespace);
        var graphLabel = profile == OpmlProfile.Graph
            ? (string?)outline.Attribute(graphNamespace + "label")
            : null;
        var label = graphLabel ?? (string?)outline.Attribute("text");
        return string.IsNullOrWhiteSpace(label) ? null : label.Trim();
    }
}
