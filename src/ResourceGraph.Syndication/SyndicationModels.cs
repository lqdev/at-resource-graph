using System.Collections.Immutable;
using ResourceGraph.Core;

namespace ResourceGraph.Syndication;

/// <summary>Bounds applied while parsing one syndication document.</summary>
public sealed class FeedParseOptions
{
    /// <summary>Creates parser limits.</summary>
    public FeedParseOptions(int maxBytes = 2 * 1024 * 1024, int maxItems = 1_000, int maxElementDepth = 64)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxItems);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxElementDepth);
        MaxBytes = maxBytes;
        MaxItems = maxItems;
        MaxElementDepth = maxElementDepth;
    }

    /// <summary>Gets conservative default limits.</summary>
    public static FeedParseOptions Default { get; } = new();

    /// <summary>Gets the maximum UTF-8 source size.</summary>
    public int MaxBytes { get; }

    /// <summary>Gets the maximum item count.</summary>
    public int MaxItems { get; }

    /// <summary>Gets the maximum XML element depth.</summary>
    public int MaxElementDepth { get; }
}

/// <summary>A media enclosure or namespace-provided media item.</summary>
public sealed class SyndicationEnclosure
{
    /// <summary>Creates an enclosure with a validated absolute URI.</summary>
    public SyndicationEnclosure(
        Uri uri,
        string? mediaType = null,
        long? length = null,
        string? duration = null,
        string? kind = null)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri)
        {
            throw new ArgumentException("An enclosure URI must be absolute.", nameof(uri));
        }

        if (length is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        Uri = uri;
        MediaType = mediaType;
        Length = length;
        Duration = duration;
        Kind = kind;
    }

    /// <summary>Gets the enclosure URI.</summary>
    public Uri Uri { get; }

    /// <summary>Gets the optional MIME type.</summary>
    public string? MediaType { get; }

    /// <summary>Gets the optional byte length.</summary>
    public long? Length { get; }

    /// <summary>Gets the optional source duration as supplied by the feed.</summary>
    public string? Duration { get; }

    /// <summary>Gets the source kind, such as enclosure, media, or thumbnail.</summary>
    public string? Kind { get; }
}

/// <summary>A normalized item from an RSS or Atom feed.</summary>
public sealed class SyndicationItem
{
    /// <summary>Creates a normalized feed item.</summary>
    public SyndicationItem(
        string? id,
        string? title,
        Uri? link,
        string? summary,
        DateTimeOffset? published,
        DateTimeOffset? updated,
        IEnumerable<SyndicationEnclosure>? enclosures = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        Id = id;
        Title = title;
        Link = link;
        Summary = summary;
        Published = published;
        Updated = updated;
        Enclosures = (enclosures ?? Array.Empty<SyndicationEnclosure>()).ToImmutableArray();
        Metadata = (metadata ?? new Dictionary<string, string>(StringComparer.Ordinal))
            .ToImmutableDictionary(StringComparer.Ordinal);
    }

    /// <summary>Gets the feed-provided item identifier.</summary>
    public string? Id { get; }

    /// <summary>Gets the item title.</summary>
    public string? Title { get; }

    /// <summary>Gets the item link.</summary>
    public Uri? Link { get; }

    /// <summary>Gets the summary or content text.</summary>
    public string? Summary { get; }

    /// <summary>Gets the publication time.</summary>
    public DateTimeOffset? Published { get; }

    /// <summary>Gets the update time.</summary>
    public DateTimeOffset? Updated { get; }

    /// <summary>Gets enclosures and namespace-provided media.</summary>
    public ImmutableArray<SyndicationEnclosure> Enclosures { get; }

    /// <summary>Gets preserved namespace-qualified metadata.</summary>
    public ImmutableDictionary<string, string> Metadata { get; }
}

/// <summary>A normalized RSS or Atom feed.</summary>
public sealed class SyndicationFeed
{
    /// <summary>Creates a normalized feed.</summary>
    public SyndicationFeed(
        SyndicationFormat format,
        string? id,
        string? title,
        Uri? link,
        string? description,
        IEnumerable<SyndicationItem>? items = null,
        IEnumerable<GraphDiagnostic>? diagnostics = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        Format = format;
        Id = id;
        Title = title;
        Link = link;
        Description = description;
        Items = (items ?? Array.Empty<SyndicationItem>()).ToImmutableArray();
        Diagnostics = (diagnostics ?? Array.Empty<GraphDiagnostic>()).ToImmutableArray();
        Metadata = (metadata ?? new Dictionary<string, string>(StringComparer.Ordinal))
            .ToImmutableDictionary(StringComparer.Ordinal);
    }

    /// <summary>Gets the source format.</summary>
    public SyndicationFormat Format { get; }

    /// <summary>Gets the feed identifier.</summary>
    public string? Id { get; }

    /// <summary>Gets the feed title.</summary>
    public string? Title { get; }

    /// <summary>Gets the feed link.</summary>
    public Uri? Link { get; }

    /// <summary>Gets the feed description.</summary>
    public string? Description { get; }

    /// <summary>Gets normalized items in source order.</summary>
    public ImmutableArray<SyndicationItem> Items { get; }

    /// <summary>Gets non-fatal parse diagnostics.</summary>
    public ImmutableArray<GraphDiagnostic> Diagnostics { get; }

    /// <summary>Gets preserved namespace-qualified feed metadata.</summary>
    public ImmutableDictionary<string, string> Metadata { get; }
}

/// <summary>Thrown when a syndication document cannot be parsed.</summary>
public class SyndicationParseException : Exception
{
    /// <summary>Creates a parse exception.</summary>
    public SyndicationParseException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when parser limits are exceeded.</summary>
public sealed class SyndicationLimitExceededException : SyndicationParseException
{
    /// <summary>Creates a limit exception.</summary>
    public SyndicationLimitExceededException(string message)
        : base(message)
    {
    }
}
