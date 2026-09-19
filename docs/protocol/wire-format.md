# Draft wire format

The draft wire format is a JSON representation of an authored bundle. It is
small by design: memberships are inline and reference envelopes carry enough
information to identify their source.

## Required shape

```json
{
  "$type": "me.lqdev.resourcegraph.temp.bundle",
  "name": "Synthetic network",
  "description": "An ordered collection.",
  "members": [
    {
      "position": 0,
      "resource": {
        "$type": "me.lqdev.resourcegraph.temp.defs#syndicationRef",
        "uri": "https://example.test/updates.xml",
        "format": "atom",
        "title": "Updates"
      }
    },
    {
      "position": 1,
      "resource": {
        "$type": "me.lqdev.resourcegraph.temp.defs#atRecordRef",
        "uri": "at://did:plc:example123/site.standard.document/3jz7document",
        "cid": "bafyexample"
      }
    }
  ]
}
```

The `$type` values identify the draft bundle and reference variants. The
serializer writes members in ascending `position` order. A reader must keep
unknown reference types inspectable rather than discard them.

## Reference fields

| Field | Applies to | Meaning |
| --- | --- | --- |
| `uri` | all references | Absolute HTTPS or AT identity text |
| `format` | `syndicationRef` | `rss` or `atom` |
| `cid` | `atRecordRef` | Optional content identifier for an observed version |
| `mode` | `bundleRef` | `live` (default) or `frozen` |
| `title` | all known references | Optional author-supplied display title |

The format does not embed feed items or AT record values. Those remain source
data resolved by an adapter. A normalized item should retain its source
reference.

## Canonical rules

Do not add a second membership record merely to share reference definitions.
The first Lexicon keeps memberships inline and puts reusable reference shapes
in `defs.json`. The complete normative draft is the local
[bundle specification](../../specs/BUNDLE.md) and
[reference specification](../../specs/REFERENCES.md).

Machine-readable files are available under the local
[draft Lexicons](../protocol/lexicons.md), and checked-in examples are linked
from the [examples reference](../reference/examples.md).
