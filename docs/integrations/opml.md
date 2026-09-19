# OPML import and export

OPML is an interoperability projection for feed-reader ecosystems. It is not
the canonical bundle format. The library supports a conventional profile and a
graph profile.

## Profiles

| Profile | Use | Preservation |
| --- | --- | --- |
| Conventional | Maximum compatibility with feed readers | RSS/Atom outlines; non-feed references may be omitted with diagnostics |
| Graph | Preserve more graph information while remaining OPML 2.0 | `rg:*` namespaced attributes for position, kind, URI, label, format, CID, and mode |

Both profiles write ordinary OPML 2.0. The graph namespace is
`https://lqdev.dev/at-resource-graph/opml`. Unknown consumers still receive a
useful `url` or `xmlUrl`.

## Loss semantics

Conventional OPML cannot preserve bundle authorship, authored membership
relationships, AT CIDs, nested-bundle mode, or full provenance. The exporter
returns an `OpmlLossReport` and the CLI writes each loss note to stderr.

The graph profile retains more fields, but keep the original bundle JSON when
you need a lossless record. Import is not a promise of semantic round-trip:
outlines that cannot become a safe typed reference produce diagnostics.

The full normative profile is the local
[OPML profile specification](../../specs/OPML-PROFILE.md).

## CLI examples

```powershell
dotnet run --project src/ResourceGraph.Cli -- import-opml tests/fixtures/opml/conventional.opml
dotnet run --project src/ResourceGraph.Cli -- export-opml examples/valid/minimal-bundle.json --profile conventional
dotnet run --project src/ResourceGraph.Cli -- export-opml examples/valid/nested-bundle.json --profile graph
```

The generated [OPML API](../reference/api.md) documents options and result
types. Import and export are bounded XML operations with DTD and external
resolver support disabled.
