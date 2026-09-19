using System.Collections.Immutable;
using System.Text.Json;
using ResourceGraph.AtProto;
using ResourceGraph.Core;
using ResourceGraph.Syndication;

namespace ResourceGraph.Bluesky;

/// <summary>Classifies a public Bluesky source reference.</summary>
public enum BlueskyReferenceKind
{
    /// <summary>An explicit public AT record.</summary>
    AtRecord,

    /// <summary>An RSS or Atom feed that remains on the Syndication path.</summary>
    Syndication,

    /// <summary>An unsupported reference.</summary>
    Unknown
}

/// <summary>Classifies a decoded public Bluesky record.</summary>
public enum BlueskyItemKind
{
    /// <summary>An app.bsky.feed.post record.</summary>
    Post,

    /// <summary>An app.bsky.actor.profile record.</summary>
    Profile
}

/// <summary>A deterministic public Bluesky item projection.</summary>
public sealed class BlueskyItem
{
    /// <summary>Creates a Bluesky item.</summary>
    public BlueskyItem(
        BlueskyItemKind kind,
        string identity,
        string authorDid,
        string? title,
        string? text,
        DateTimeOffset? published,
        Uri link)
    {
        Kind = kind;
        Identity = identity;
        AuthorDid = authorDid;
        Title = title;
        Text = text;
        Published = published;
        Link = link;
    }

    /// <summary>Gets the item kind.</summary>
    public BlueskyItemKind Kind { get; }

    /// <summary>Gets the stable AT identity.</summary>
    public string Identity { get; }

    /// <summary>Gets the author DID.</summary>
    public string AuthorDid { get; }

    /// <summary>Gets the deterministic display title.</summary>
    public string? Title { get; }

    /// <summary>Gets the post text or profile description.</summary>
    public string? Text { get; }

    /// <summary>Gets the source creation time.</summary>
    public DateTimeOffset? Published { get; }

    /// <summary>Gets the public Bluesky web link.</summary>
    public Uri Link { get; }
}

/// <summary>Reads public Bluesky record shapes and keeps feeds on Syndication.</summary>
public static class BlueskyAdapter
{
    /// <summary>Classifies a resource reference without performing I/O.</summary>
    public static BlueskyReferenceKind Classify(ResourceReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference switch
        {
            AtRecordReference atRecord when atRecord.Uri is not null &&
                (atRecord.Identity.Contains("/app.bsky.feed.post/", StringComparison.Ordinal) ||
                 atRecord.Identity.Contains("/app.bsky.actor.profile/", StringComparison.Ordinal))
                => BlueskyReferenceKind.AtRecord,
            SyndicationReference => BlueskyReferenceKind.Syndication,
            _ => BlueskyReferenceKind.Unknown
        };
    }

    /// <summary>Projects a public post or profile envelope deterministically.</summary>
    public static OperationResult<BlueskyItem> Project(AtRecordEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var diagnostics = ImmutableArray.CreateBuilder<GraphDiagnostic>();
        try
        {
            return envelope.Uri.Collection switch
            {
                "app.bsky.feed.post" => ProjectPost(envelope, diagnostics),
                "app.bsky.actor.profile" => ProjectProfile(envelope, diagnostics),
                _ => Unsupported(envelope, diagnostics)
            };
        }
        catch (JsonException exception)
        {
            diagnostics.Add(new GraphDiagnostic(
                "bluesky.record.invalid",
                exception.Message,
                DiagnosticSeverity.Error,
                envelope.Uri.ToString()));
            return new OperationResult<BlueskyItem>(null, diagnostics);
        }
    }

    /// <summary>Returns a Syndication feed unchanged so RSS remains the feed path.</summary>
    public static SyndicationFeed KeepSyndicationPath(SyndicationFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);
        return feed;
    }

    private static OperationResult<BlueskyItem> ProjectPost(
        AtRecordEnvelope envelope,
        ImmutableArray<GraphDiagnostic>.Builder diagnostics)
    {
        var text = RequiredString(envelope.Value, "text");
        var createdAt = OptionalDate(envelope.Value, "createdAt", diagnostics, envelope.Uri.ToString());
        var title = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        var link = BuildPostLink(envelope.Uri);
        return new OperationResult<BlueskyItem>(
            new BlueskyItem(
                BlueskyItemKind.Post,
                envelope.Uri.ToString(),
                envelope.Uri.Did,
                title,
                text,
                createdAt,
                link),
            diagnostics);
    }

    private static OperationResult<BlueskyItem> ProjectProfile(
        AtRecordEnvelope envelope,
        ImmutableArray<GraphDiagnostic>.Builder diagnostics)
    {
        var displayName = OptionalString(envelope.Value, "displayName");
        var description = OptionalString(envelope.Value, "description");
        if (displayName is null && description is null)
        {
            diagnostics.Add(new GraphDiagnostic(
                "bluesky.profile.empty",
                "The profile has neither displayName nor description.",
                DiagnosticSeverity.Warning,
                envelope.Uri.ToString()));
        }

        return new OperationResult<BlueskyItem>(
            new BlueskyItem(
                BlueskyItemKind.Profile,
                envelope.Uri.ToString(),
                envelope.Uri.Did,
                displayName ?? envelope.Uri.Did,
                description,
                null,
                new Uri($"https://bsky.app/profile/{Uri.EscapeDataString(envelope.Uri.Did)}")),
            diagnostics);
    }

    private static OperationResult<BlueskyItem> Unsupported(
        AtRecordEnvelope envelope,
        ImmutableArray<GraphDiagnostic>.Builder diagnostics)
    {
        diagnostics.Add(new GraphDiagnostic(
            "bluesky.record.unsupported",
            $"Collection '{envelope.Uri.Collection}' is not supported by this adapter.",
            DiagnosticSeverity.Warning,
            envelope.Uri.ToString()));
        return new OperationResult<BlueskyItem>(null, diagnostics);
    }

    private static string RequiredString(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new JsonException($"The Bluesky record requires a non-empty '{propertyName}'.");
        }

        return property.GetString()!;
    }

    private static string? OptionalString(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    }

    private static DateTimeOffset? OptionalDate(
        JsonElement value,
        string propertyName,
        ImmutableArray<GraphDiagnostic>.Builder diagnostics,
        string identity)
    {
        var text = OptionalString(value, propertyName);
        if (text is null)
        {
            return null;
        }

        if (DateTimeOffset.TryParse(text, out var date))
        {
            return date;
        }

        diagnostics.Add(new GraphDiagnostic(
            "bluesky.date.invalid",
            $"The value '{text}' is not a valid {propertyName}.",
            DiagnosticSeverity.Warning,
            identity));
        return null;
    }

    private static Uri BuildPostLink(AtUri uri) =>
        new($"https://bsky.app/profile/{Uri.EscapeDataString(uri.Did)}/post/{uri.Rkey}");
}
