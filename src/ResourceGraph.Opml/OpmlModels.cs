using System.Collections.Immutable;
using ResourceGraph.Core;

namespace ResourceGraph.Opml;

/// <summary>OPML projection profile.</summary>
public enum OpmlProfile
{
    /// <summary>Conventional feed-reader-compatible projection.</summary>
    Conventional,

    /// <summary>Namespaced projection that retains graph-specific fields.</summary>
    Graph
}

/// <summary>Documents semantics that a conventional OPML export cannot retain.</summary>
public sealed class OpmlLossReport
{
    /// <summary>Creates a loss report.</summary>
    public OpmlLossReport(
        bool authorshipLost,
        bool membershipRelationshipsLost,
        bool cidsLost,
        bool nestedBundlesLost,
        bool provenanceLost,
        IEnumerable<string>? notes = null)
    {
        AuthorshipLost = authorshipLost;
        MembershipRelationshipsLost = membershipRelationshipsLost;
        CidsLost = cidsLost;
        NestedBundlesLost = nestedBundlesLost;
        ProvenanceLost = provenanceLost;
        Notes = (notes ?? Array.Empty<string>()).ToImmutableArray();
    }

    /// <summary>Gets whether bundle authorship was lost.</summary>
    public bool AuthorshipLost { get; }

    /// <summary>Gets whether authored membership edges were lost.</summary>
    public bool MembershipRelationshipsLost { get; }

    /// <summary>Gets whether AT CIDs were lost.</summary>
    public bool CidsLost { get; }

    /// <summary>Gets whether nested bundle semantics were lost.</summary>
    public bool NestedBundlesLost { get; }

    /// <summary>Gets whether provenance was lost.</summary>
    public bool ProvenanceLost { get; }

    /// <summary>Gets human-readable loss notes.</summary>
    public ImmutableArray<string> Notes { get; }
}

/// <summary>An OPML export and its explicit loss report.</summary>
public sealed class OpmlExportResult
{
    /// <summary>Creates an OPML export result.</summary>
    public OpmlExportResult(string xml, OpmlProfile profile, OpmlLossReport lossReport)
    {
        Xml = string.IsNullOrWhiteSpace(xml)
            ? throw new ArgumentException("OPML XML is required.", nameof(xml))
            : xml;
        Profile = profile;
        LossReport = lossReport ?? throw new ArgumentNullException(nameof(lossReport));
    }

    /// <summary>Gets the serialized OPML.</summary>
    public string Xml { get; }

    /// <summary>Gets the source profile.</summary>
    public OpmlProfile Profile { get; }

    /// <summary>Gets the explicit projection loss report.</summary>
    public OpmlLossReport LossReport { get; }
}

/// <summary>Bounds applied while importing an OPML document.</summary>
public sealed class OpmlImportOptions
{
    /// <summary>Creates OPML import limits.</summary>
    public OpmlImportOptions(int maxBytes = 2 * 1024 * 1024, int maxOutlines = 10_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxOutlines);
        MaxBytes = maxBytes;
        MaxOutlines = maxOutlines;
    }

    /// <summary>Gets default OPML limits.</summary>
    public static OpmlImportOptions Default { get; } = new();

    /// <summary>Gets the maximum UTF-8 input size.</summary>
    public int MaxBytes { get; }

    /// <summary>Gets the maximum outline count.</summary>
    public int MaxOutlines { get; }
}

/// <summary>An imported bundle plus explicit diagnostics.</summary>
public sealed class OpmlImportResult
{
    /// <summary>Creates an OPML import result.</summary>
    public OpmlImportResult(Bundle? bundle, IEnumerable<GraphDiagnostic>? diagnostics)
    {
        Bundle = bundle;
        Diagnostics = (diagnostics ?? Array.Empty<GraphDiagnostic>()).ToImmutableArray();
    }

    /// <summary>Gets the partially imported bundle, when one could be built.</summary>
    public Bundle? Bundle { get; }

    /// <summary>Gets parse and validation diagnostics.</summary>
    public ImmutableArray<GraphDiagnostic> Diagnostics { get; }

    /// <summary>Gets whether the import produced no error diagnostics.</summary>
    public bool IsSuccess => Bundle is not null &&
        Diagnostics.All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error);
}
