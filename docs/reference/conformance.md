# Conformance

Conformance is fixture-driven and offline. An implementation report should
identify its profile and policy, then run the checked-in vectors.

| Level | Demonstrates |
| --- | --- |
| Syntax | Lexicon and example JSON parse without a network |
| Bundle | Inline order, reference kind, and nested provenance |
| Bounded expansion | Depth, member, byte, and item limits with diagnostics |
| Projection | Both OPML profiles and explicit loss reporting |
| Security | Endpoint, parser, and rendering checks before external resolution |

The current executable slice implements syntax checks, Core validation,
RSS/Atom parsing, bounded head discovery, nested expansion diagnostics, and
both OPML profiles using offline fixtures. It does not claim OAuth, firehose,
AppView, browser, or live-network conformance.

Read the local [canonical conformance document](../../specs/CONFORMANCE.md) and
the [fixture README](../../specs/conformance/README.md). The bootstrap tests
are the practical regression suite for this slice.
