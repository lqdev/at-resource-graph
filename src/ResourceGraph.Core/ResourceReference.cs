namespace ResourceGraph.Core;

/// <summary>Identifies the protocol or source represented by a resource reference.</summary>
public enum ResourceReferenceKind
{
    /// <summary>An RSS or Atom feed.</summary>
    Syndication,

    /// <summary>An AT Protocol record.</summary>
    AtRecord,

    /// <summary>A nested resource bundle.</summary>
    NestedBundle,

    /// <summary>An HTTPS URL whose metadata can be discovered.</summary>
    ExplicitDiscovery,

    /// <summary>A reference preserved without a known decoder.</summary>
    Unknown
}

/// <summary>Identifies a syndication format.</summary>
public enum SyndicationFormat
{
    /// <summary>RSS 2.0.</summary>
    Rss,

    /// <summary>Atom.</summary>
    Atom
}

/// <summary>Identifies whether a nested bundle is live or a frozen observation.</summary>
public enum BundleReferenceMode
{
    /// <summary>Resolve the current bundle when expanding.</summary>
    Live,

    /// <summary>Resolve the referenced snapshot without refreshing it.</summary>
    Frozen
}

/// <summary>A validated, immutable reference to a graph resource.</summary>
public abstract record ResourceReference
{
    /// <summary>Gets the reference kind.</summary>
    public abstract ResourceReferenceKind Kind { get; }

    /// <summary>Gets a stable identity used for cycle detection and deduplication.</summary>
    public abstract string Identity { get; }

    /// <summary>Gets the URI when the reference has one.</summary>
    public virtual Uri? Uri => null;

    /// <summary>Gets a display title, when supplied by the author.</summary>
    public virtual string? Title => null;

    /// <summary>Validates an absolute URI and its allowed schemes.</summary>
    protected static Uri ValidateUri(Uri uri, string parameterName, params string[] allowedSchemes)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri)
        {
            throw new ArgumentException("The URI must be absolute.", parameterName);
        }

        if (allowedSchemes.Length == 0 ||
            !allowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"The URI scheme '{uri.Scheme}' is not allowed for this reference.",
                parameterName);
        }

        return uri;
    }

    /// <summary>Validates a bounded optional title.</summary>
    protected static string? ValidateTitle(string? title)
    {
        if (title is not null && (string.IsNullOrWhiteSpace(title) || title.Length > 512))
        {
            throw new ArgumentException("A title must be non-empty and at most 512 characters.", nameof(title));
        }

        return title;
    }
}

/// <summary>A reference to an RSS or Atom feed.</summary>
public sealed record SyndicationReference : ResourceReference
{
    /// <summary>Creates a validated syndication reference.</summary>
    public SyndicationReference(Uri uri, SyndicationFormat format, string? title = null)
    {
        Uri = ValidateUri(uri, nameof(uri), Uri.UriSchemeHttps);
        Format = format;
        Title = ValidateTitle(title);
    }

    /// <summary>Gets the HTTPS feed URI.</summary>
    public override Uri Uri { get; }

    /// <summary>Gets the feed format.</summary>
    public SyndicationFormat Format { get; }

    /// <inheritdoc />
    public override string? Title { get; }

    /// <inheritdoc />
    public override ResourceReferenceKind Kind => ResourceReferenceKind.Syndication;

    /// <inheritdoc />
    public override string Identity => Uri.AbsoluteUri;
}

/// <summary>A reference to an AT Protocol record.</summary>
public sealed record AtRecordReference : ResourceReference
{
    /// <summary>Creates a validated AT URI reference.</summary>
    public AtRecordReference(Uri uri, string? cid = null, string? title = null)
    {
        Uri = ValidateUri(uri, nameof(uri), "at");
        AtUri = ResourceUri.CanonicalText(Uri);
        Cid = ValidateCid(cid);
        Title = ValidateTitle(title);
    }

    /// <summary>Creates a validated AT URI reference from canonical text.</summary>
    public AtRecordReference(string uri, string? cid = null, string? title = null)
    {
        Uri = ValidateUri(ResourceUri.Create(uri), nameof(uri), "at");
        AtUri = ResourceUri.CanonicalText(uri);
        Cid = ValidateCid(cid);
        Title = ValidateTitle(title);
    }

    /// <summary>Gets the AT URI.</summary>
    public override Uri Uri { get; }

    /// <summary>Gets the canonical AT URI text.</summary>
    public string AtUri { get; }

    /// <summary>Gets the optional content identifier for a frozen observation.</summary>
    public string? Cid { get; }

    /// <inheritdoc />
    public override string? Title { get; }

    /// <inheritdoc />
    public override ResourceReferenceKind Kind => ResourceReferenceKind.AtRecord;

    /// <inheritdoc />
    public override string Identity => AtUri;

    private static string? ValidateCid(string? cid)
    {
        if (cid is not null && (string.IsNullOrWhiteSpace(cid) || cid.Length > 256))
        {
            throw new ArgumentException("A CID must be non-empty and at most 256 characters.", nameof(cid));
        }

        return cid;
    }
}

/// <summary>A reference to another bundle.</summary>
public sealed record NestedBundleReference : ResourceReference
{
    /// <summary>Creates a validated nested bundle reference.</summary>
    public NestedBundleReference(
        Uri uri,
        BundleReferenceMode mode = BundleReferenceMode.Live,
        string? title = null)
    {
        Uri = ValidateUri(uri, nameof(uri), Uri.UriSchemeHttps, "at");
        BundleUri = ResourceUri.CanonicalText(Uri);
        Mode = mode;
        Title = ValidateTitle(title);
    }

    /// <summary>Creates a validated nested bundle reference from canonical text.</summary>
    public NestedBundleReference(
        string uri,
        BundleReferenceMode mode = BundleReferenceMode.Live,
        string? title = null)
    {
        Uri = ValidateUri(ResourceUri.Create(uri), nameof(uri), Uri.UriSchemeHttps, "at");
        BundleUri = ResourceUri.CanonicalText(uri);
        Mode = mode;
        Title = ValidateTitle(title);
    }

    /// <summary>Gets the bundle URI.</summary>
    public override Uri Uri { get; }

    /// <summary>Gets the canonical bundle URI text.</summary>
    public string BundleUri { get; }

    /// <summary>Gets the resolution mode.</summary>
    public BundleReferenceMode Mode { get; }

    /// <inheritdoc />
    public override string? Title { get; }

    /// <inheritdoc />
    public override ResourceReferenceKind Kind => ResourceReferenceKind.NestedBundle;

    /// <inheritdoc />
    public override string Identity => BundleUri;
}

/// <summary>A reference to an HTTPS page whose metadata may be discovered.</summary>
public sealed record ExplicitDiscoveryReference : ResourceReference
{
    /// <summary>Creates a validated HTTPS discovery reference.</summary>
    public ExplicitDiscoveryReference(Uri uri, string? title = null)
    {
        Uri = ValidateUri(uri, nameof(uri), Uri.UriSchemeHttps);
        Title = ValidateTitle(title);
    }

    /// <summary>Gets the HTTPS page URI.</summary>
    public override Uri Uri { get; }

    /// <inheritdoc />
    public override string? Title { get; }

    /// <inheritdoc />
    public override ResourceReferenceKind Kind => ResourceReferenceKind.ExplicitDiscovery;

    /// <inheritdoc />
    public override string Identity => Uri.AbsoluteUri;
}

/// <summary>Preserves a resource that this version of the graph does not decode.</summary>
public sealed record UnknownResourceReference : ResourceReference
{
    /// <summary>Creates an inspectable unknown reference.</summary>
    public UnknownResourceReference(string identity, string resourceType, string? rawValue = null)
    {
        if (string.IsNullOrWhiteSpace(identity))
        {
            throw new ArgumentException("An unknown resource identity is required.", nameof(identity));
        }

        if (string.IsNullOrWhiteSpace(resourceType))
        {
            throw new ArgumentException("An unknown resource type is required.", nameof(resourceType));
        }

        Identity = identity;
        ResourceType = resourceType;
        RawValue = rawValue;
    }

    /// <summary>Gets the stable identity supplied by the source.</summary>
    public override string Identity { get; }

    /// <summary>Gets the source-specific resource type.</summary>
    public string ResourceType { get; }

    /// <summary>Gets the optional raw value retained for inspection.</summary>
    public string? RawValue { get; }

    /// <inheritdoc />
    public override ResourceReferenceKind Kind => ResourceReferenceKind.Unknown;
}
