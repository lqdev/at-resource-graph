using System.Collections.Immutable;
using ResourceGraph.AtProto;
using ResourceGraph.Core;
using ResourceGraph.Discovery;

namespace ResourceGraph.StandardSite;

/// <summary>A validated Standard.site declaration discovered from metadata.</summary>
public sealed class StandardSiteDeclaration
{
    /// <summary>Creates a Standard.site declaration.</summary>
    public StandardSiteDeclaration(
        AtUri document,
        AtUri? publication = null,
        AtUri? author = null,
        AtUri? me = null,
        IEnumerable<GraphDiagnostic>? diagnostics = null)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Publication = publication;
        Author = author;
        Me = me;
        Diagnostics = (diagnostics ?? Array.Empty<GraphDiagnostic>()).ToImmutableArray();
    }

    /// <summary>Gets the Standard.site document record.</summary>
    public AtUri Document { get; }

    /// <summary>Gets the optional publication record.</summary>
    public AtUri? Publication { get; }

    /// <summary>Gets the optional author record.</summary>
    public AtUri? Author { get; }

    /// <summary>Gets the optional AT identity record.</summary>
    public AtUri? Me { get; }

    /// <summary>Gets metadata validation diagnostics.</summary>
    public ImmutableArray<GraphDiagnostic> Diagnostics { get; }
}

/// <summary>Adapts existing head-discovery links without fetching a page body.</summary>
public static class StandardSiteMetadataAdapter
{
    /// <summary>Builds a declaration from a Discovery result.</summary>
    public static OperationResult<StandardSiteDeclaration> FromDiscovery(DiscoveryResult discovery)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        var diagnostics = discovery.Diagnostics.ToBuilder();
        var documentLink = discovery.Links.FirstOrDefault(
            link => link.Kind == DiscoveryLinkKind.StandardSiteVerification) ??
            discovery.Links.FirstOrDefault(link => link.Kind == DiscoveryLinkKind.AtCanonical);

        if (documentLink is null)
        {
            diagnostics.Add(new GraphDiagnostic(
                "standardsite.document.missing",
                "No Standard.site document or AT canonical link was discovered.",
                DiagnosticSeverity.Warning));
            return new OperationResult<StandardSiteDeclaration>(null, diagnostics);
        }

        var document = ParseAt(documentLink, "standardsite.document.invalid", diagnostics);
        if (document is null)
        {
            return new OperationResult<StandardSiteDeclaration>(null, diagnostics);
        }

        var publication = ParseOptional(
            discovery.Links.FirstOrDefault(link => link.Kind == DiscoveryLinkKind.AtAlternate),
            "standardsite.publication.invalid",
            diagnostics);
        var author = ParseOptional(
            discovery.Links.FirstOrDefault(link => link.Kind == DiscoveryLinkKind.AtAuthor),
            "standardsite.author.invalid",
            diagnostics);
        var me = ParseOptional(
            discovery.Links.FirstOrDefault(link => link.Kind == DiscoveryLinkKind.AtMe),
            "standardsite.me.invalid",
            diagnostics);

        return new OperationResult<StandardSiteDeclaration>(
            new StandardSiteDeclaration(document, publication, author, me, diagnostics),
            diagnostics);
    }

    private static AtUri? ParseOptional(
        DiscoveredLink? link,
        string code,
        ImmutableArray<GraphDiagnostic>.Builder diagnostics) =>
        link is null ? null : ParseAt(link, code, diagnostics);

    private static AtUri? ParseAt(
        DiscoveredLink link,
        string code,
        ImmutableArray<GraphDiagnostic>.Builder diagnostics)
    {
        try
        {
            return AtUri.Parse(link.UriText);
        }
        catch (FormatException exception)
        {
            diagnostics.Add(new GraphDiagnostic(
                code,
                exception.Message,
                DiagnosticSeverity.Error,
                link.UriText));
            return null;
        }
    }
}
