namespace ResourceGraph.Core;

/// <summary>Parses ordinary and AT URIs without losing AT DID authority text.</summary>
public static class ResourceUri
{
    /// <summary>Creates an absolute URI or throws an explicit argument error.</summary>
    public static Uri Create(string value, string parameterName = "uri")
    {
        if (!TryCreate(value, out var uri))
        {
            throw new ArgumentException("The value must be an absolute URI.", parameterName);
        }

        return uri;
    }

    /// <summary>Parses an absolute URI, including AT URIs with colon-bearing DIDs.</summary>
    public static bool TryCreate(string? value, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed) && parsed is not null)
        {
            uri = parsed;
            return true;
        }

        if (!trimmed.StartsWith("at://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var authorityAndPath = trimmed["at://".Length..];
        var slash = authorityAndPath.IndexOf('/');
        var authority = slash < 0 ? authorityAndPath : authorityAndPath[..slash];
        var path = slash < 0 ? "/" : authorityAndPath[slash..];
        if (authority.Length == 0 || authority.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var surrogateAuthority = authority.Replace(':', '-');
        var escaped = $"at://{surrogateAuthority}{path}";
        if (Uri.TryCreate(escaped, UriKind.Absolute, out parsed) && parsed is not null)
        {
            uri = parsed;
            return true;
        }

        return false;
    }

    /// <summary>Returns the canonical text form, unescaping an encoded AT authority.</summary>
    public static string CanonicalText(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.Scheme.Equals("at", StringComparison.OrdinalIgnoreCase))
        {
            return uri.AbsoluteUri;
        }

        var authority = Uri.UnescapeDataString(uri.Authority);
        return $"at://{authority}{uri.PathAndQuery}{uri.Fragment}";
    }

    /// <summary>Returns the canonical text form of a URI value.</summary>
    public static string CanonicalText(string value)
    {
        if (!TryCreate(value, out _))
        {
            throw new ArgumentException("The value must be an absolute URI.", nameof(value));
        }

        return value.Trim();
    }
}
