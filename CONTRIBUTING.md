# Contributing

Thank you for helping build an interoperable resource graph. Keep changes
small, documented, and independently testable.

## Before opening a change

1. Read [GOVERNANCE.md](GOVERNANCE.md) and the relevant specification.
2. Use synthetic identities and URLs in tests and examples.
3. Do not make live network calls from unit tests.
4. Run restore, build, and the focused test project.
5. Update normative prose when behavior or wire shapes change.

The `me.lqdev.resourcegraph.temp` Lexicons are draft incubation material.
Schema changes must include a fixture or an explicit compatibility note.

## Scope discipline

The first usable implementation will be protocol-independent in Core and will
add adapters at the boundary. Do not add protocol, persistence, UI, or service
dependencies to `ResourceGraph.Core`.

## Pull requests

Describe the interoperability problem, the proposed behavior, compatibility
impact, and validation performed. A pull request should not silently turn
metadata discovery into content scraping.
