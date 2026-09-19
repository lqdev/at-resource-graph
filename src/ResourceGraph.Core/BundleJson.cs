using System.Collections.Immutable;
using System.Text.Json;

namespace ResourceGraph.Core;

/// <summary>Parses and writes the draft inline bundle JSON shape.</summary>
public static class BundleJson
{
    private const string BundleType = "me.lqdev.resourcegraph.temp.bundle";
    private const string SyndicationType = "me.lqdev.resourcegraph.temp.defs#syndicationRef";
    private const string AtRecordType = "me.lqdev.resourcegraph.temp.defs#atRecordRef";
    private const string NestedBundleType = "me.lqdev.resourcegraph.temp.defs#bundleRef";
    private const string DiscoveryType = "resourcegraph.explicitDiscoveryRef";
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    /// <summary>Parses one bundle without network access.</summary>
    public static Bundle Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Bundle JSON is required.", nameof(json));
        }

        using var document = JsonDocument.Parse(json);
        return ReadBundle(document.RootElement);
    }

    /// <summary>Serializes one bundle using deterministic property order and indentation.</summary>
    public static string Serialize(Bundle bundle, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var root = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["$type"] = BundleType,
            ["name"] = bundle.Name
        };

        if (bundle.Description is not null)
        {
            root["description"] = bundle.Description;
        }

        if (bundle.SelfUriText is not null)
        {
            root["uri"] = bundle.SelfUriText;
        }

        root["members"] = bundle.Members
            .OrderBy(member => member.Position)
            .Select(member =>
            {
                var item = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["position"] = member.Position,
                    ["resource"] = WriteReference(member.Resource)
                };

                if (member.Label is not null)
                {
                    item["label"] = member.Label;
                }

                return item;
            })
            .ToArray();

        if (indented)
        {
            return JsonSerializer.Serialize(root, SerializerOptions);
        }

        return JsonSerializer.Serialize(root);
    }

    private static Bundle ReadBundle(JsonElement root)
    {
        RequireKind(root, JsonValueKind.Object, "The bundle must be a JSON object.");
        if (root.TryGetProperty("$type", out var type) &&
            !string.Equals(type.GetString(), BundleType, StringComparison.Ordinal))
        {
            throw new JsonException($"Expected bundle type '{BundleType}'.");
        }

        var name = RequiredString(root, "name");
        var description = OptionalString(root, "description");
        var selfUriText = OptionalString(root, "uri");
        if (!root.TryGetProperty("members", out var members) || members.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Bundle property 'members' must be an array.");
        }

        var parsedMembers = ImmutableArray.CreateBuilder<Membership>();
        foreach (var member in members.EnumerateArray())
        {
            RequireKind(member, JsonValueKind.Object, "Each membership must be an object.");
            if (!member.TryGetProperty("position", out var position) ||
                position.ValueKind != JsonValueKind.Number ||
                !position.TryGetInt32(out var positionValue))
            {
                throw new JsonException("Each membership requires an integer 'position'.");
            }

            if (!member.TryGetProperty("resource", out var resource))
            {
                throw new JsonException("Each membership requires a 'resource'.");
            }

            parsedMembers.Add(new Membership(
                positionValue,
                ReadReference(resource),
                OptionalString(member, "label")));
        }

        return new Bundle(
            name,
            parsedMembers,
            description,
            selfUriText is null ? null : ResourceUri.Create(selfUriText),
            selfUriText);
    }

    private static ResourceReference ReadReference(JsonElement resource)
    {
        RequireKind(resource, JsonValueKind.Object, "A resource reference must be an object.");
        var type = RequiredString(resource, "$type");
        var uriValue = OptionalString(resource, "uri");
        if (uriValue is null || !ResourceUri.TryCreate(uriValue, out var uri))
        {
            throw new JsonException("A resource reference requires an absolute 'uri'.");
        }

        return type switch
        {
            SyndicationType => ReadSyndication(resource, uri),
            AtRecordType => new AtRecordReference(uriValue, OptionalString(resource, "cid"), OptionalString(resource, "title")),
            NestedBundleType => ReadNestedBundle(resource, uriValue),
            DiscoveryType => new ExplicitDiscoveryReference(uri, OptionalString(resource, "title")),
            _ => new UnknownResourceReference(uri.AbsoluteUri, type, resource.GetRawText())
        };
    }

    private static SyndicationReference ReadSyndication(JsonElement resource, Uri uri)
    {
        var format = RequiredString(resource, "format").ToLowerInvariant() switch
        {
            "rss" => SyndicationFormat.Rss,
            "atom" => SyndicationFormat.Atom,
            _ => throw new JsonException("Syndication format must be 'rss' or 'atom'.")
        };

        return new SyndicationReference(uri, format, OptionalString(resource, "title"));
    }

    private static NestedBundleReference ReadNestedBundle(JsonElement resource, string uri)
    {
        var mode = OptionalString(resource, "mode")?.ToLowerInvariant() switch
        {
            null or "live" => BundleReferenceMode.Live,
            "frozen" => BundleReferenceMode.Frozen,
            _ => throw new JsonException("Bundle reference mode must be 'live' or 'frozen'.")
        };

        return new NestedBundleReference(uri, mode, OptionalString(resource, "title"));
    }

    private static Dictionary<string, object?> WriteReference(ResourceReference reference)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["$type"] = reference switch
            {
                SyndicationReference => SyndicationType,
                AtRecordReference => AtRecordType,
                NestedBundleReference => NestedBundleType,
                ExplicitDiscoveryReference => DiscoveryType,
                _ => $"unknown:{reference.Kind.ToString().ToLowerInvariant()}"
            },
            ["uri"] = ReferenceText(reference)
        };

        switch (reference)
        {
            case SyndicationReference syndication:
                result["format"] = syndication.Format.ToString().ToLowerInvariant();
                break;
            case AtRecordReference atRecord when atRecord.Cid is not null:
                result["cid"] = atRecord.Cid;
                break;
            case NestedBundleReference nested:
                result["mode"] = nested.Mode.ToString().ToLowerInvariant();
                break;
            case UnknownResourceReference unknown when unknown.RawValue is not null:
                result["rawValue"] = unknown.RawValue;
                break;
        }

        if (reference.Title is not null)
        {
            result["title"] = reference.Title;
        }

        return result;
    }

    private static string ReferenceText(ResourceReference reference) =>
        reference switch
        {
            AtRecordReference atRecord => atRecord.AtUri,
            NestedBundleReference nested => nested.BundleUri,
            _ => reference.Uri?.AbsoluteUri ?? reference.Identity
        };

    private static string RequiredString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new JsonException($"Property '{propertyName}' must be a non-empty string.");
        }

        return value.GetString()!;
    }

    private static string? OptionalString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"Property '{propertyName}' must be a string.");
        }

        return value.GetString();
    }

    private static void RequireKind(JsonElement element, JsonValueKind expected, string message)
    {
        if (element.ValueKind != expected)
        {
            throw new JsonException(message);
        }
    }
}
