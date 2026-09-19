using System.Text.RegularExpressions;

namespace ResourceGraph.AtProto;

/// <summary>Strict validation helpers for AT Protocol identifiers.</summary>
public static partial class AtProtoIdentifierValidation
{
    /// <summary>Validates a DID and throws when it is not a supported AT authority.</summary>
    public static string ValidateDid(string did)
    {
        if (!IsValidDid(did))
        {
            throw new ArgumentException("The value must be a valid DID.", nameof(did));
        }

        return did;
    }

    /// <summary>Returns whether a DID has a valid method and identifier shape.</summary>
    public static bool IsValidDid(string? did) =>
        !string.IsNullOrWhiteSpace(did) &&
        did.Length <= 256 &&
        DidRegex().IsMatch(did);

    /// <summary>Validates a handle and throws when it is not DNS-like.</summary>
    public static string ValidateHandle(string handle)
    {
        if (!IsValidHandle(handle))
        {
            throw new ArgumentException("The value must be a valid AT handle.", nameof(handle));
        }

        return handle.ToLowerInvariant();
    }

    /// <summary>Returns whether a handle has valid lowercase DNS labels.</summary>
    public static bool IsValidHandle(string? handle) =>
        !string.IsNullOrWhiteSpace(handle) &&
        handle.Length <= 253 &&
        HandleRegex().IsMatch(handle);

    /// <summary>Validates a collection NSID.</summary>
    public static string ValidateCollection(string collection)
    {
        if (!IsValidCollection(collection))
        {
            throw new ArgumentException("The value must be a valid collection NSID.", nameof(collection));
        }

        return collection;
    }

    /// <summary>Returns whether a collection is a lowercase dotted NSID.</summary>
    public static bool IsValidCollection(string? collection) =>
        !string.IsNullOrWhiteSpace(collection) &&
        collection.Length <= 256 &&
        CollectionRegex().IsMatch(collection);

    /// <summary>Validates a CID-like strong reference string.</summary>
    public static string ValidateCid(string cid)
    {
        if (string.IsNullOrWhiteSpace(cid) || cid.Length > 256 || !CidRegex().IsMatch(cid))
        {
            throw new ArgumentException("The value must be a valid CID string.", nameof(cid));
        }

        return cid;
    }

    [GeneratedRegex(@"^did:[a-z0-9]+:[A-Za-z0-9._:%-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex DidRegex();

    [GeneratedRegex(@"^(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex HandleRegex();

    [GeneratedRegex(@"^[a-z0-9-]+(?:\.[a-z0-9-]+){2,}$", RegexOptions.CultureInvariant)]
    private static partial Regex CollectionRegex();

    [GeneratedRegex(@"^[A-Za-z0-9._:-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CidRegex();
}
