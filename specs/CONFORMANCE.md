# Conformance

This draft uses fixture-driven conformance. A future implementation report
SHOULD identify the profile and policy used, then run the fixtures in
`specs/conformance/fixtures`.

## Levels

| Level | Requirement |
| --- | --- |
| Syntax | Parse the checked-in Lexicon and example JSON without network access. |
| Bundle | Preserve inline order, reference kind, and nested membership provenance. |
| Bounded expansion | Enforce depth, member, byte, and item limits and report diagnostics. |
| Projection | Implement both OPML profiles and document lossiness. |
| Security | Apply the checks in [SECURITY.md](SECURITY.md) before external resolution. |

The first executable slice implements syntax checks, Core validation,
RSS/Atom parsing, bounded head discovery, nested expansion diagnostics, and
both OPML import/export profiles using offline fixtures. It does not claim
OAuth, firehose, AppView, browser, or live-network conformance.
