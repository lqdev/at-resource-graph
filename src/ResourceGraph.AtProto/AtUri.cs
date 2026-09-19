using ResourceGraph.Core;

namespace ResourceGraph.AtProto;

/// <summary>A strict, formatter-preserving AT Protocol record URI.</summary>
public sealed record AtUri
{
    /// <summary>Creates a validated AT record URI.</summary>
    public AtUri(string did, string collection, string rkey, string? cid = null)
    {
        Did = AtProtoIdentifierValidation.ValidateDid(did);
        Collection = AtProtoIdentifierValidation.ValidateCollection(collection);
        if (string.IsNullOrWhiteSpace(rkey) ||
            rkey.Length > 512 ||
            rkey.Contains('/') ||
            rkey.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("The value must be a valid AT record key.", nameof(rkey));
        }

        Rkey = rkey;
        Cid = cid is null ? null : AtProtoIdentifierValidation.ValidateCid(cid);
    }

    /// <summary>Gets the DID authority, preserving its colons.</summary>
    public string Did { get; }

    /// <summary>Gets the collection NSID.</summary>
    public string Collection { get; }

    /// <summary>Gets the record key.</summary>
    public string Rkey { get; }

    /// <summary>Gets the optional CID query value.</summary>
    public string? Cid { get; }

    /// <summary>Parses an AT URI or throws a format exception.</summary>
    public static AtUri Parse(string value)
    {
        if (!TryParse(value, out var result))
        {
            throw new FormatException("The value is not a valid AT record URI.");
        }

        return result;
    }

    /// <summary>Attempts to parse an AT URI without using System.Uri authority parsing.</summary>
    public static bool TryParse(string? value, out AtUri result)
    {
        result = null!;
        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith("at://", StringComparison.Ordinal) ||
            value.Contains('#'))
        {
            return false;
        }

        var remainder = value["at://".Length..];
        var queryIndex = remainder.IndexOf('?');
        var query = queryIndex < 0 ? null : remainder[(queryIndex + 1)..];
        var path = queryIndex < 0 ? remainder : remainder[..queryIndex];
        if (query is not null &&
            (!query.StartsWith("cid=", StringComparison.Ordinal) ||
             query.Contains('&')))
        {
            return false;
        }

        var slash = path.IndexOf('/');
        if (slash <= 0 || slash == path.Length - 1)
        {
            return false;
        }

        var did = path[..slash];
        var recordPath = path[(slash + 1)..];
        var recordParts = recordPath.Split('/', StringSplitOptions.None);
        if (recordParts.Length != 2)
        {
            return false;
        }

        var cid = query is null
            ? null
            : Uri.UnescapeDataString(query["cid=".Length..]);
        try
        {
            result = new AtUri(did, recordParts[0], recordParts[1], cid);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Returns canonical AT URI text with an optional CID query.</summary>
    public override string ToString() =>
        Cid is null
            ? $"at://{Did}/{Collection}/{Rkey}"
            : $"at://{Did}/{Collection}/{Rkey}?cid={Uri.EscapeDataString(Cid)}";

    /// <summary>Returns the Core URI surrogate for APIs that require System.Uri.</summary>
    public Uri ToResourceUri() => ResourceUri.Create(ToString());
}
