using System.Collections.Immutable;

namespace ResourceGraph.Core;

/// <summary>An ordered edge from a bundle to one resource reference.</summary>
public sealed record Membership
{
    /// <summary>Creates an immutable membership.</summary>
    public Membership(int position, ResourceReference resource, string? label = null)
    {
        if (position < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Membership positions cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(resource);

        if (label is not null && (string.IsNullOrWhiteSpace(label) || label.Length > 512))
        {
            throw new ArgumentException("A membership label must be non-empty and at most 512 characters.", nameof(label));
        }

        Position = position;
        Resource = resource;
        Label = label;
    }

    /// <summary>Gets the authored position.</summary>
    public int Position { get; }

    /// <summary>Gets the referenced resource.</summary>
    public ResourceReference Resource { get; }

    /// <summary>Gets the optional author label.</summary>
    public string? Label { get; }
}

/// <summary>An authored collection of inline ordered memberships.</summary>
public sealed record Bundle
{
    /// <summary>Creates a validated immutable bundle.</summary>
    public Bundle(
        string name,
        IEnumerable<Membership> members,
        string? description = null,
        Uri? selfUri = null,
        string? selfUriText = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 512)
        {
            throw new ArgumentException("A bundle name is required and must be at most 512 characters.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(members);

        var materialized = members.ToImmutableArray();
        if (materialized.IsDefault)
        {
            throw new ArgumentException("Bundle memberships must be materialized.", nameof(members));
        }

        if (materialized.Length > 100_000)
        {
            throw new ArgumentException("A bundle cannot contain more than 100,000 memberships.", nameof(members));
        }

        if (materialized.GroupBy(member => member.Position).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Bundle membership positions must be unique.", nameof(members));
        }

        if (description is not null && description.Length > 4_000)
        {
            throw new ArgumentException("A bundle description must be at most 4,000 characters.", nameof(description));
        }

        if (selfUri is not null && !selfUri.IsAbsoluteUri)
        {
            throw new ArgumentException("A bundle self URI must be absolute.", nameof(selfUri));
        }

        Uri? parsedSelfUri = null;
        if (selfUriText is not null &&
            (!ResourceUri.TryCreate(selfUriText, out parsedSelfUri) || parsedSelfUri is null))
        {
            throw new ArgumentException("A bundle self URI text must be absolute.", nameof(selfUriText));
        }

        Name = name;
        Description = description;
        Members = materialized;
        SelfUri = selfUri ?? (selfUriText is null ? null : parsedSelfUri);
        SelfUriText = selfUriText ?? SelfUri?.AbsoluteUri;
    }

    /// <summary>Gets the bundle name.</summary>
    public string Name { get; }

    /// <summary>Gets the optional bundle description.</summary>
    public string? Description { get; }

    /// <summary>Gets the immutable authored membership list.</summary>
    public ImmutableArray<Membership> Members { get; }

    /// <summary>Gets the optional bundle URI.</summary>
    public Uri? SelfUri { get; }

    /// <summary>Gets the canonical source text for the optional bundle URI.</summary>
    public string? SelfUriText { get; }

    /// <summary>Gets the stable identity used for resolution and provenance.</summary>
    public string Identity => SelfUriText ?? $"bundle:{Name}";
}

/// <summary>A single step in the authored provenance path.</summary>
public sealed record ProvenanceStep
{
    /// <summary>Creates a provenance step.</summary>
    public ProvenanceStep(string bundleIdentity, int position, string resourceIdentity)
    {
        if (string.IsNullOrWhiteSpace(bundleIdentity))
        {
            throw new ArgumentException("A bundle identity is required.", nameof(bundleIdentity));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(position);

        if (string.IsNullOrWhiteSpace(resourceIdentity))
        {
            throw new ArgumentException("A resource identity is required.", nameof(resourceIdentity));
        }

        BundleIdentity = bundleIdentity;
        Position = position;
        ResourceIdentity = resourceIdentity;
    }

    /// <summary>Gets the containing bundle identity.</summary>
    public string BundleIdentity { get; }

    /// <summary>Gets the membership position.</summary>
    public int Position { get; }

    /// <summary>Gets the resource identity.</summary>
    public string ResourceIdentity { get; }
}

/// <summary>Captures the complete path and source metadata for a resolved resource.</summary>
public sealed record Provenance
{
    /// <summary>Creates immutable provenance.</summary>
    public Provenance(
        IEnumerable<ProvenanceStep>? path = null,
        Uri? sourceUri = null,
        Uri? atUri = null,
        string? authorDid = null,
        string? publisher = null)
    {
        Path = (path ?? Array.Empty<ProvenanceStep>()).ToImmutableArray();
        SourceUri = sourceUri;
        AtUri = atUri;
        AuthorDid = authorDid;
        Publisher = publisher;
    }

    /// <summary>Gets the ordered membership path.</summary>
    public ImmutableArray<ProvenanceStep> Path { get; }

    /// <summary>Gets the source URI, when known.</summary>
    public Uri? SourceUri { get; }

    /// <summary>Gets the AT URI, when known.</summary>
    public Uri? AtUri { get; }

    /// <summary>Gets the author DID, when known.</summary>
    public string? AuthorDid { get; }

    /// <summary>Gets the publisher label, when known.</summary>
    public string? Publisher { get; }

    /// <summary>Returns a copy with one additional membership step.</summary>
    public Provenance Append(Bundle bundle, Membership membership) =>
        new(
            Path.Add(new ProvenanceStep(
                bundle.Identity,
                membership.Position,
                membership.Resource.Identity)),
            SourceUri,
            AtUri,
            AuthorDid,
            Publisher);
}

/// <summary>A resource emitted by bounded bundle expansion.</summary>
public sealed record ExpandedResource
{
    /// <summary>Creates an expanded resource.</summary>
    public ExpandedResource(ResourceReference reference, Provenance provenance)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(provenance);
        Reference = reference;
        Provenance = provenance;
    }

    /// <summary>Gets the emitted reference.</summary>
    public ResourceReference Reference { get; }

    /// <summary>Gets the complete authored provenance.</summary>
    public Provenance Provenance { get; }
}
