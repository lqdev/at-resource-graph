# Resource model

The graph has four useful concepts: a bundle, an inline membership, a
reference envelope, and provenance.

## Bundle and membership

```json
{
  "$type": "me.lqdev.resourcegraph.temp.bundle",
  "name": "Reading list",
  "members": [
    {
      "position": 0,
      "label": "Example feed",
      "resource": {
        "$type": "me.lqdev.resourcegraph.temp.defs#syndicationRef",
        "uri": "https://example.test/updates.xml",
        "format": "rss"
      }
    }
  ]
}
```

`name` and `members` are required. `description` and a bundle `uri` are
optional. Each membership has a unique non-negative `position` and exactly one
`resource`. A consumer may normalize gaps in positions, but must preserve
relative authored order.

## Reference kinds

| Draft reference | Use |
| --- | --- |
| `syndicationRef` | An HTTPS RSS or Atom feed, with `format` set to `rss` or `atom` |
| `atRecordRef` | An `at://` record, optionally pinned with a CID |
| `bundleRef` | Another bundle, with `live` or `frozen` mode |
| unknown or future type | An inspectable envelope retained without a decoder |

An HTTPS page discovered through metadata is an implementation-level
`ExplicitDiscoveryReference`; it is not a replacement for a typed feed or AT
reference.

## Provenance

An expanded resource carries its source reference and the complete sequence of
bundle identity, position, and resource identity steps that led to it. Preserve
that path when rendering or exporting. It is graph meaning, not display-only
metadata.

For the normative field and lifecycle rules, see the local
[reference model](../../specs/REFERENCES.md) and
[bundle model](../../specs/BUNDLE.md).
