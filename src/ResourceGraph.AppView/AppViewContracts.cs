using ResourceGraph.Core;

namespace ResourceGraph.AppView;

/// <summary>Read-only contract for a future public AppView projection.</summary>
public interface IResourceGraphAppView
{
    /// <summary>Gets a public bundle by stable identity.</summary>
    ValueTask<Bundle?> GetPublicBundleAsync(
        string bundleIdentity,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a bounded public expansion.</summary>
    ValueTask<OperationResult<ExpandedGraphSnapshot>> ExpandPublicBundleAsync(
        string bundleIdentity,
        ExpansionLimits limits,
        CancellationToken cancellationToken = default);
}

/// <summary>A transport-neutral future AppView expansion snapshot.</summary>
public sealed class ExpandedGraphSnapshot
{
    /// <summary>Creates an immutable AppView snapshot.</summary>
    public ExpandedGraphSnapshot(
        IEnumerable<ExpandedResource> resources,
        IEnumerable<GraphDiagnostic>? diagnostics = null)
    {
        Resources = resources?.ToArray() ?? throw new ArgumentNullException(nameof(resources));
        Diagnostics = (diagnostics ?? Array.Empty<GraphDiagnostic>()).ToArray();
    }

    /// <summary>Gets expanded resources.</summary>
    public IReadOnlyList<ExpandedResource> Resources { get; }

    /// <summary>Gets expansion diagnostics.</summary>
    public IReadOnlyList<GraphDiagnostic> Diagnostics { get; }
}
