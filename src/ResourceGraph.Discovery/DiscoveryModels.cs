using System.Collections.Immutable;
using System.Net;
using ResourceGraph.Core;

namespace ResourceGraph.Discovery;

/// <summary>Classifies a metadata link discovered in an HTML head.</summary>
public enum DiscoveryLinkKind
{
    /// <summary>An RSS alternate feed.</summary>
    Rss,

    /// <summary>An Atom alternate feed.</summary>
    Atom,

    /// <summary>An OPML outline link.</summary>
    Opml,

    /// <summary>A Standard.site verification record.</summary>
    StandardSiteVerification,

    /// <summary>An AT canonical record.</summary>
    AtCanonical,

    /// <summary>An AT alternate record.</summary>
    AtAlternate,

    /// <summary>An AT author reference.</summary>
    AtAuthor,

    /// <summary>An AT identity reference.</summary>
    AtMe
}

/// <summary>Policy for bounded and safe HTML-head discovery.</summary>
public sealed class DiscoveryPolicy
{
    /// <summary>Creates a validated discovery policy.</summary>
    public DiscoveryPolicy(int maxHeadBytes = 64 * 1024, bool requireHttps = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHeadBytes);
        MaxHeadBytes = maxHeadBytes;
        RequireHttps = requireHttps;
    }

    /// <summary>Gets conservative default discovery policy.</summary>
    public static DiscoveryPolicy Default { get; } = new();

    /// <summary>Gets the maximum UTF-8 bytes inspected before head termination.</summary>
    public int MaxHeadBytes { get; }

    /// <summary>Gets whether external destinations must use HTTPS.</summary>
    public bool RequireHttps { get; }

    /// <summary>Validates a discovered URI for its link context.</summary>
    public bool IsAllowed(Uri uri, bool allowAtUri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (allowAtUri && uri.Scheme.Equals("at", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) || RequireHttps))
        {
            return false;
        }

        return !IsPrivateOrMetadataDestination(uri.Host);
    }

    /// <summary>Returns whether a host is loopback, private, link-local, or metadata-only.</summary>
    public static bool IsPrivateOrMetadataDestination(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return true;
        }

        var normalizedHost = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (normalizedHost is "localhost" or "metadata" or "metadata.google.internal" or "instance-data.ec2.internal" ||
            normalizedHost.EndsWith(".localhost", StringComparison.Ordinal) ||
            normalizedHost.EndsWith(".local", StringComparison.Ordinal) ||
            normalizedHost.EndsWith(".internal", StringComparison.Ordinal))
        {
            return true;
        }

        if (!IPAddress.TryParse(normalizedHost, out var address))
        {
            return false;
        }

        if (IPAddress.IsLoopback(address) ||
            address.IsIPv6LinkLocal ||
            address.IsIPv6SiteLocal)
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        if (bytes.Length == 16)
        {
            return (bytes[0] & 0xfe) == 0xfc;
        }

        return bytes.Length == 4 &&
            (bytes[0] == 0 ||
             bytes[0] == 10 ||
             bytes[0] == 127 ||
             (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) ||
             (bytes[0] == 169 && bytes[1] == 254) ||
             (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
             (bytes[0] == 192 && bytes[1] == 168));
    }
}

/// <summary>A safe URI discovered from metadata.</summary>
public sealed class DiscoveredLink
{
    /// <summary>Creates a discovered metadata link.</summary>
    public DiscoveredLink(
        DiscoveryLinkKind kind,
        Uri uri,
        string? rel = null,
        string? mediaType = null,
        string? title = null,
        string? uriText = null)
    {
        ArgumentNullException.ThrowIfNull(uri);
        Kind = kind;
        Uri = uri;
        Rel = rel;
        MediaType = mediaType;
        Title = title;
        UriText = uriText ?? uri.AbsoluteUri;
    }

    /// <summary>Gets the link kind.</summary>
    public DiscoveryLinkKind Kind { get; }

    /// <summary>Gets the discovered URI.</summary>
    public Uri Uri { get; }

    /// <summary>Gets the original URI text, including canonical AT URI syntax.</summary>
    public string UriText { get; }

    /// <summary>Gets the optional relation value.</summary>
    public string? Rel { get; }

    /// <summary>Gets the optional media type.</summary>
    public string? MediaType { get; }

    /// <summary>Gets the optional title.</summary>
    public string? Title { get; }
}

/// <summary>The result of bounded metadata-only head discovery.</summary>
public sealed class DiscoveryResult
{
    /// <summary>Creates a discovery result.</summary>
    public DiscoveryResult(
        IEnumerable<DiscoveredLink>? links,
        IEnumerable<GraphDiagnostic>? diagnostics,
        int scannedBytes,
        bool reachedHeadEnd)
    {
        Links = (links ?? Array.Empty<DiscoveredLink>()).ToImmutableArray();
        Diagnostics = (diagnostics ?? Array.Empty<GraphDiagnostic>()).ToImmutableArray();
        ScannedBytes = scannedBytes;
        ReachedHeadEnd = reachedHeadEnd;
    }

    /// <summary>Gets links emitted from the head only.</summary>
    public ImmutableArray<DiscoveredLink> Links { get; }

    /// <summary>Gets explicit parse and policy diagnostics.</summary>
    public ImmutableArray<GraphDiagnostic> Diagnostics { get; }

    /// <summary>Gets the UTF-8 bytes inspected.</summary>
    public int ScannedBytes { get; }

    /// <summary>Gets whether a closing head tag terminated scanning.</summary>
    public bool ReachedHeadEnd { get; }
}

/// <summary>Thrown for invalid discovery input or policy configuration.</summary>
public sealed class DiscoveryPolicyException : Exception
{
    /// <summary>Creates a discovery policy exception.</summary>
    public DiscoveryPolicyException(string message)
        : base(message)
    {
    }
}
