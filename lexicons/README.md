# Draft Lexicons

The JSON files under `me/lqdev/resourcegraph/temp` use Lexicon language version
1 and are intentionally marked as draft incubation material in their
descriptions and this document. Lexicon 1 does not define a formal top-level
`status` field, so draft status is documented externally rather than by adding
an invalid schema property.

The bundle record keeps ordered memberships inline. Shared reference shapes are
in `defs.json` so later Lexicons can reuse them without inventing a second
membership record for the initial slice. The membership resource union is
inline because Lexicon 1 does not allow a named union definition.
