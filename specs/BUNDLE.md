# Bundle model

A bundle is an authored collection record with a name, optional description,
and an inline `members` array. Each member has a non-negative `position` and a
single `resource` reference. Positions express authored order; consumers MUST
use the array and position together to preserve deterministic ordering and MAY
normalize gaps without changing relative order.

The initial Lexicon is
`me.lqdev.resourcegraph.temp.bundle`. Memberships are inline rather than
separate records. This keeps the first interoperable projection small and makes
the edge visible when a bundle is read.

## Nested bundles

A `bundleRef` is a membership like any other resource reference. A consumer
MAY expand it when policy permits and MUST preserve both the parent membership
and the nested path in provenance. A cycle, limit, unavailable source, or
unsupported record is an explicit diagnostic, not an instruction to drop the
edge.

## Ownership operations

Add, delete, reorder, import, subscribe, unsubscribe, publish, and export act
on membership edges. Deleting an edge MUST NOT delete the target resource.
Export to OPML is intentionally lossy; see [OPML-PROFILE.md](OPML-PROFILE.md).
