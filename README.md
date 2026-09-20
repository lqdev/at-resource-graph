# AT Resource Graph

AT Resource Graph is a standalone .NET 10 project for composing protocol-aware
resource collections from AT Protocol records, RSS/Atom feeds, websites, and
nested bundles. Reusable libraries are published as immutable preview packages
to `github.com/lqdev/at-resource-graph`.

## Bootstrap status

This checkout implements Milestone 0 and the beginning of Milestone 1:

- A clean .NET solution with protocol, composition, presentation, AppView, and
  application project boundaries.
- Draft resource Lexicons under the temporary `me.lqdev.resourcegraph.temp`
  namespace.
- Normative specification and conformance-document skeletons.
- A DocFX-compatible documentation site and GitHub Pages workflow.
- JSON examples and a focused bootstrap test project.

Runtime graph expansion, feed parsing, OAuth, firehose ingestion, and AppView
projection are intentionally not implemented in this slice. The first
public-read adapters now include AT record envelopes, Standard.site metadata,
Bluesky post/profile projections, and an offline Reader reference surface.

## Repository layout

| Path | Purpose |
| --- | --- |
| `src/` | .NET libraries and application entry points |
| `tests/` | Focused bootstrap and future conformance tests |
| `lexicons/` | Draft machine-readable AT Protocol schemas |
| `specs/` | Normative graph, bundle, reference, OPML, and security prose |
| `examples/` | Valid and intentionally schema-invalid JSON examples |
| `docs/` | DocFX source and editorial profile |

`ResourceGraph.Core` is protocol-independent. It must not acquire service,
database, web framework, UI, or AT SDK dependencies as the project grows.

`ResourceGraph.AtProto` performs public reads only. `ResourceGraph.StandardSite`
and `ResourceGraph.Bluesky` consume validated metadata or record envelopes.
`ResourceGraph.Reader` serves only its local sample data in this slice; it does
not accept a URL and does not fetch arbitrary resources.

Public AT reads construct their own no-proxy, no-redirect socket transport.
Each connection callback resolves and validates the destination, then connects
only to that validated IP set while preserving the original hostname for
TLS/SNI. Arbitrary `HttpMessageHandler` chains are rejected; offline tests use
the separate `CreateForTesting` transport boundary.

## Local validation

```powershell
dotnet restore ResourceGraph.sln
dotnet build ResourceGraph.sln --no-restore
dotnet test tests/ResourceGraph.Bootstrap.Tests/ResourceGraph.Bootstrap.Tests.csproj --no-restore
```

The examples use synthetic URLs and identities. Tests do not call external
feeds or AT services.

## Reusable packages

The reusable libraries are published to GitHub Packages as
`ResourceGraph.Core`, `ResourceGraph.AtProto`, `ResourceGraph.Syndication`,
`ResourceGraph.Discovery`, `ResourceGraph.StandardSite`, `ResourceGraph.Bluesky`,
`ResourceGraph.Composition`, `ResourceGraph.Opml`, and `ResourceGraph.AppView`.
The initial version is `0.1.0-preview.1`. See
[ResourceGraph package consumption and publishing](docs/getting-started/packages.md)
for exact authentication, restore, AppView sample, and release commands.

The first executable offline slice is available through
`ResourceGraph.Cli`:

```powershell
dotnet run --project src/ResourceGraph.Cli -- validate examples/valid/minimal-bundle.json
dotnet run --project src/ResourceGraph.Cli -- parse-feed tests/fixtures/syndication/rss.xml
dotnet run --project src/ResourceGraph.Cli -- discover-head tests/fixtures/discovery/head.html
dotnet run --project src/ResourceGraph.Cli -- import-opml tests/fixtures/opml/conventional.opml
dotnet run --project src/ResourceGraph.Cli -- export-opml examples/valid/minimal-bundle.json --profile graph
dotnet run --project src/ResourceGraph.Cli -- expand examples/valid/minimal-bundle.json
```

All commands are deterministic and offline. Expansion reports an explicit
source-unavailable diagnostic when a nested bundle has no supplied resolver.

## Namespace and governance

The `me.lqdev.resourcegraph.temp` namespace is an incubation namespace. It is
not a claim to a permanent public standard. A future move to
`community.lexicon.*` requires demonstrated interoperability, independent
implementations, public discussion, and the community.lexicon governance gate.
See [GOVERNANCE.md](GOVERNANCE.md) and [specs/SPEC.md](specs/SPEC.md).
