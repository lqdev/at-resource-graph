# Reference model

The draft definitions Lexicon contains three reusable reference shapes:

| Reference | Meaning |
| --- | --- |
| `syndicationRef` | An RSS or Atom feed URI and its format |
| `atRecordRef` | An AT URI, with an optional CID for a frozen observation |
| `bundleRef` | Another bundle, with `live` or `frozen` mode |

These are reference envelopes, not feed-item schemas. Adapters resolve a
reference into a source or normalized resource. A source reference MUST be
preserved when an item is normalized.

The `resource` field in an inline membership is a union of the three shapes.
Implementations MUST retain an unknown or future reference envelope as an
inspectable generic value when they cannot resolve it.

## Provenance

Each normalized item SHOULD expose its source reference, source URI, optional
AT URI and CID, author or publisher when available, and the complete ordered
membership path. Provenance is part of graph meaning, not optional display
decoration.

## Public AT records

An AT record reference has a strict textual `at://` form with a DID authority,
collection NSID, record key, and optional CID query. Implementations MUST
preserve DID colons when formatting the identity. A public `getRecord` response
MUST include a valid AT URI, CID, and object value before an adapter exposes it
to downstream projections.
