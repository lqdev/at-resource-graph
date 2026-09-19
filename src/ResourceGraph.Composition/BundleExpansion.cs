using System.Collections.Immutable;
using ResourceGraph.Core;

namespace ResourceGraph.Composition;

/// <summary>Resolves nested bundle references for one bounded expansion.</summary>
public interface IBundleResolver
{
    /// <summary>Resolves a nested bundle reference without changing the reference.</summary>
    ValueTask<Bundle?> ResolveAsync(NestedBundleReference reference, CancellationToken cancellationToken);
}

/// <summary>An in-memory resolver useful for offline fixtures and deterministic tests.</summary>
public sealed class InMemoryBundleResolver : IBundleResolver
{
    private readonly ImmutableDictionary<string, Bundle> bundles;

    /// <summary>Creates an in-memory resolver indexed by each bundle self URI or name.</summary>
    public InMemoryBundleResolver(IEnumerable<Bundle> bundles)
    {
        ArgumentNullException.ThrowIfNull(bundles);
        this.bundles = bundles
            .Select(bundle => new
            {
                Key = bundle.Identity,
                Bundle = bundle
            })
            .ToImmutableDictionary(item => item.Key, item => item.Bundle, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public ValueTask<Bundle?> ResolveAsync(
        NestedBundleReference reference,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bundles.TryGetValue(reference.Identity, out var bundle);
        return ValueTask.FromResult(bundle);
    }
}

/// <summary>Result of a bounded nested-bundle expansion.</summary>
public sealed class BundleExpansionResult
{
    /// <summary>Creates an expansion result.</summary>
    public BundleExpansionResult(
        IEnumerable<ExpandedResource>? resources,
        IEnumerable<GraphDiagnostic>? diagnostics)
    {
        Resources = (resources ?? Array.Empty<ExpandedResource>()).ToImmutableArray();
        Diagnostics = (diagnostics ?? Array.Empty<GraphDiagnostic>()).ToImmutableArray();
    }

    /// <summary>Gets unique leaf resources in deterministic order.</summary>
    public ImmutableArray<ExpandedResource> Resources { get; }

    /// <summary>Gets cycle, limit, source, and unknown-resource diagnostics.</summary>
    public ImmutableArray<GraphDiagnostic> Diagnostics { get; }

    /// <summary>Gets whether no error diagnostic was produced.</summary>
    public bool IsComplete => Diagnostics.All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error);
}

/// <summary>Expands nested bundles with explicit bounds, provenance, and cycle detection.</summary>
public sealed class BundleExpander
{
    /// <summary>Expands a root bundle.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1822",
        Justification = "The instance API leaves room for resolver and policy injection in later milestones.")]
    public async ValueTask<BundleExpansionResult> ExpandAsync(
        Bundle root,
        IBundleResolver resolver,
        ExpansionLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(resolver);

        var effectiveLimits = limits ?? ExpansionLimits.Default;
        var resources = ImmutableArray.CreateBuilder<ExpandedResource>();
        var diagnostics = ImmutableArray.CreateBuilder<GraphDiagnostic>();
        var seenResources = new HashSet<string>(StringComparer.Ordinal);
        var activeBundles = new HashSet<string>(StringComparer.Ordinal)
        {
            BundleIdentity(root)
        };

        await ExpandBundleAsync(
            root,
            new Provenance(),
            0,
            resolver,
            effectiveLimits,
            resources,
            diagnostics,
            seenResources,
            activeBundles,
            cancellationToken);

        return new BundleExpansionResult(resources, diagnostics);
    }

    private static async ValueTask ExpandBundleAsync(
        Bundle bundle,
        Provenance provenance,
        int depth,
        IBundleResolver resolver,
        ExpansionLimits limits,
        ImmutableArray<ExpandedResource>.Builder resources,
        ImmutableArray<GraphDiagnostic>.Builder diagnostics,
        HashSet<string> seenResources,
        HashSet<string> activeBundles,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (bundle.Members.Length > limits.MaxMembersPerBundle)
        {
            diagnostics.Add(new GraphDiagnostic(
                "expansion.member-limit",
                $"Bundle '{bundle.Name}' has {bundle.Members.Length} members; the limit is {limits.MaxMembersPerBundle}.",
                DiagnosticSeverity.Error,
                BundleIdentity(bundle)));
        }

        foreach (var membership in DeterministicGraph.OrderMemberships(bundle.Members)
                     .Take(limits.MaxMembersPerBundle))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var memberProvenance = provenance.Append(bundle, membership);
            var reference = membership.Resource;

            if (reference is NestedBundleReference nested)
            {
                if (depth >= limits.MaxDepth)
                {
                    diagnostics.Add(new GraphDiagnostic(
                        "expansion.depth-limit",
                        $"Nested bundle expansion reached the depth limit of {limits.MaxDepth}.",
                        DiagnosticSeverity.Error,
                        nested.Identity,
                        membership.Position));
                    continue;
                }

                if (!activeBundles.Add(nested.Identity))
                {
                    diagnostics.Add(new GraphDiagnostic(
                        "expansion.cycle",
                        $"Nested bundle '{nested.Identity}' is already active.",
                        DiagnosticSeverity.Error,
                        nested.Identity,
                        membership.Position));
                    continue;
                }

                var nestedBundle = await resolver.ResolveAsync(nested, cancellationToken);
                if (nestedBundle is null)
                {
                    diagnostics.Add(new GraphDiagnostic(
                        "expansion.source-unavailable",
                        $"No bundle was available for '{nested.Identity}'.",
                        DiagnosticSeverity.Error,
                        nested.Identity,
                        membership.Position));
                    activeBundles.Remove(nested.Identity);
                    continue;
                }

                await ExpandBundleAsync(
                    nestedBundle,
                    memberProvenance,
                    depth + 1,
                    resolver,
                    limits,
                    resources,
                    diagnostics,
                    seenResources,
                    activeBundles,
                    cancellationToken);
                activeBundles.Remove(nested.Identity);
                continue;
            }

            if (reference is UnknownResourceReference)
            {
                diagnostics.Add(new GraphDiagnostic(
                    "resource.unknown",
                    "An unknown resource was preserved as an inspectable leaf.",
                    DiagnosticSeverity.Warning,
                    reference.Identity,
                    membership.Position));
            }

            if (resources.Count >= limits.MaxExpandedItems)
            {
                diagnostics.Add(new GraphDiagnostic(
                    "expansion.output-limit",
                    $"Expansion reached the output limit of {limits.MaxExpandedItems} resources.",
                    DiagnosticSeverity.Error,
                    reference.Identity,
                    membership.Position));
                return;
            }

            if (!seenResources.Add(reference.Identity))
            {
                diagnostics.Add(new GraphDiagnostic(
                    "expansion.duplicate",
                    $"Duplicate resource '{reference.Identity}' was omitted after its first occurrence.",
                    DiagnosticSeverity.Info,
                    reference.Identity,
                    membership.Position));
                continue;
            }

            resources.Add(new ExpandedResource(reference, memberProvenance));
        }
    }

    private static string BundleIdentity(Bundle bundle) =>
        bundle.Identity;
}
