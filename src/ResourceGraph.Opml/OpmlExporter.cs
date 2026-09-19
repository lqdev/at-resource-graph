using System.Xml.Linq;
using ResourceGraph.Core;

namespace ResourceGraph.Opml;

/// <summary>Exports Core bundles to conventional or graph OPML.</summary>
public static class OpmlExporter
{
    /// <summary>The namespace used by the graph OPML profile.</summary>
    public const string GraphNamespace = "https://lqdev.dev/at-resource-graph/opml";
    private static readonly string[] ConventionalLossNotes =
    [
        "The conventional profile cannot represent bundle authorship.",
        "The conventional profile flattens membership relationships into outlines.",
        "AT CIDs, nested bundle semantics, and provenance are not represented."
    ];

    /// <summary>Exports a bundle with an explicit loss report.</summary>
    public static OpmlExportResult Export(Bundle bundle, OpmlProfile profile)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var graphNamespace = XNamespace.Get(GraphNamespace);
        var root = new XElement(
            "opml",
            new XAttribute("version", "2.0"),
            profile == OpmlProfile.Graph
                ? new XAttribute(XNamespace.Xmlns + "rg", GraphNamespace)
                : null,
            new XElement(
                "head",
                new XElement("title", bundle.Name),
                bundle.Description is null ? null : new XElement("description", bundle.Description)),
            new XElement(
                "body",
                DeterministicGraph.OrderMemberships(bundle.Members)
                    .Select(member => CreateOutline(member, profile, graphNamespace))));

        var lossReport = profile == OpmlProfile.Conventional
            ? new OpmlLossReport(
                authorshipLost: true,
                membershipRelationshipsLost: true,
                cidsLost: true,
                nestedBundlesLost: true,
                provenanceLost: true,
                ConventionalLossNotes)
            : new OpmlLossReport(
                authorshipLost: false,
                membershipRelationshipsLost: false,
                cidsLost: false,
                nestedBundlesLost: false,
                provenanceLost: false);

        return new OpmlExportResult(
            new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root).ToString(),
            profile,
            lossReport);
    }

    private static XElement CreateOutline(
        Membership membership,
        OpmlProfile profile,
        XNamespace graphNamespace)
    {
        var reference = membership.Resource;
        var outline = new XElement(
            "outline",
            new XAttribute("text", membership.Label ?? reference.Title ?? reference.Identity));

        if (reference is SyndicationReference syndication)
        {
            outline.SetAttributeValue("type", "rss");
            outline.SetAttributeValue("xmlUrl", syndication.Uri.AbsoluteUri);
        }
        else
        {
            outline.SetAttributeValue("type", "link");
            outline.SetAttributeValue("url", ReferenceText(reference));
        }

        if (profile == OpmlProfile.Graph)
        {
            outline.SetAttributeValue(graphNamespace + "position", membership.Position);
            outline.SetAttributeValue(graphNamespace + "kind", KindName(reference));
            outline.SetAttributeValue(graphNamespace + "uri", ReferenceText(reference));
            if (membership.Label is not null)
            {
                outline.SetAttributeValue(graphNamespace + "label", membership.Label);
            }

            switch (reference)
            {
                case SyndicationReference syndicationReference:
                    outline.SetAttributeValue(
                        graphNamespace + "format",
                        syndicationReference.Format.ToString().ToLowerInvariant());
                    break;
                case AtRecordReference atRecord when atRecord.Cid is not null:
                    outline.SetAttributeValue(graphNamespace + "cid", atRecord.Cid);
                    break;
                case NestedBundleReference nested:
                    outline.SetAttributeValue(graphNamespace + "mode", nested.Mode.ToString().ToLowerInvariant());
                    break;
                case UnknownResourceReference unknown:
                    outline.SetAttributeValue(graphNamespace + "type", unknown.ResourceType);
                    break;
            }
        }

        return outline;
    }

    private static string KindName(ResourceReference reference) =>
        reference switch
        {
            SyndicationReference => "syndication",
            AtRecordReference => "at-record",
            NestedBundleReference => "nested-bundle",
            ExplicitDiscoveryReference => "discovery",
            UnknownResourceReference => "unknown",
            _ => "unknown"
        };

    private static string ReferenceText(ResourceReference reference) =>
        reference switch
        {
            AtRecordReference atRecord => atRecord.AtUri,
            NestedBundleReference nested => nested.BundleUri,
            _ => reference.Uri?.AbsoluteUri ?? reference.Identity
        };
}
