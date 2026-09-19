# Draft Lexicons

The checked-in Lexicons are language-version-1 incubation artifacts. They are
the machine-readable source for the current draft shape, but they are not a
published community namespace.

## Files

| File | Purpose |
| --- | --- |
| [`bundle.json`](../../lexicons/me/lqdev/resourcegraph/temp/bundle.json) | The `me.lqdev.resourcegraph.temp.bundle` record |
| [`defs.json`](../../lexicons/me/lqdev/resourcegraph/temp/defs.json) | `syndicationRef`, `atRecordRef`, `bundleRef`, and `membership` definitions |
| [Lexicon notes](../../lexicons/README.md) | Why draft status is documented outside the Lexicon object |

The Lexicon uses inline memberships because Lexicon 1 does not allow a named
union definition for the `resource` field. Reusable reference definitions live
in `defs.json`. The descriptions explicitly say “draft incubation”; do not
infer a formal status field that Lexicon 1 does not define.

## Namespace and compatibility

The `me.lqdev.resourcegraph.temp` namespace is intentionally temporary.
Implementations should preserve the `$type` string and unknown fields when
round-tripping. Moving a proven subset to `community.lexicon.*` requires public
discussion, independent implementations, conformance fixtures, and the
[governance gate](../../GOVERNANCE.md).

The wire-level behavior is summarized in the
[draft wire format](wire-format.md). The normative status and scope are in the
[canonical specification](../../specs/SPEC.md).
