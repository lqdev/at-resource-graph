using System.Collections.Immutable;

namespace ResourceGraph.Core;

/// <summary>Deterministic ordering and identity helpers for graph collections.</summary>
public static class DeterministicGraph
{
    /// <summary>Orders memberships by authored position and stable identity.</summary>
    public static ImmutableArray<Membership> OrderMemberships(IEnumerable<Membership> memberships)
    {
        ArgumentNullException.ThrowIfNull(memberships);

        return memberships
            .OrderBy(member => member.Position)
            .ThenBy(member => member.Resource.Identity, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    /// <summary>Deduplicates references by stable identity while preserving first occurrence.</summary>
    public static ImmutableArray<ResourceReference> DeduplicateReferences(
        IEnumerable<ResourceReference> references,
        ISet<string>? seen = null)
    {
        ArgumentNullException.ThrowIfNull(references);

        var identities = seen ?? new HashSet<string>(StringComparer.Ordinal);
        var result = ImmutableArray.CreateBuilder<ResourceReference>();
        foreach (var reference in references)
        {
            ArgumentNullException.ThrowIfNull(reference);
            if (identities.Add(reference.Identity))
            {
                result.Add(reference);
            }
        }

        return result.ToImmutable();
    }
}
