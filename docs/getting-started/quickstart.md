# Quickstart

This quickstart uses only checked-in files. It does not fetch a feed, resolve
an AT record, or contact a network service.

## Prerequisites

- .NET SDK `10.0.401`, as selected by `global.json`.
- The pinned DocFX tool is optional for using the CLI and is restored from
  `.config/dotnet-tools.json`.

From the repository root, restore and build the solution:

```powershell
dotnet restore ResourceGraph.sln
dotnet build ResourceGraph.sln --no-restore
```

## Validate a bundle

The smallest valid example contains one ordered RSS reference:

```powershell
dotnet run --project src/ResourceGraph.Cli -- validate examples/valid/minimal-bundle.json
```

Expected output begins with:

```text
valid: examples/valid/minimal-bundle.json
```

Try the intentionally invalid fixture to see a non-zero validation result:

```powershell
dotnet run --project src/ResourceGraph.Cli -- validate examples/invalid/missing-position.json
```

The invalid file is valid JSON but omits the required membership `position`.
The CLI reports the error instead of silently repairing the record.

## Parse a feed fixture

RSS and Atom are parsed into the same normalized model. Choose a local fixture
to inspect the result:

```powershell
dotnet run --project src/ResourceGraph.Cli -- parse-feed tests/fixtures/syndication/rss.xml
dotnet run --project src/ResourceGraph.Cli -- parse-feed tests/fixtures/syndication/atom.xml
```

The result includes the source format, feed identity, item count, item links,
publication times, and enclosure counts. See [RSS and Atom integration](../integrations/syndication.md)
for limits and preserved metadata.

## Import and export OPML

Import a conventional OPML file:

```powershell
dotnet run --project src/ResourceGraph.Cli -- import-opml tests/fixtures/opml/conventional.opml
```

Export a bundle using the graph profile:

```powershell
dotnet run --project src/ResourceGraph.Cli -- export-opml examples/valid/nested-bundle.json --profile graph --output .\bundle.opml
```

The exporter writes a loss report to stderr. Keep the bundle JSON beside an
OPML file when you need graph authorship, CIDs, or nested provenance. Read
[OPML profiles](../integrations/opml.md) before treating an export as a
round-trip.

## Expand a nested bundle

Expansion uses an injected resolver. The CLI intentionally supplies an empty
offline resolver, so the nested reference produces an explicit
source-unavailable diagnostic:

```powershell
dotnet run --project src/ResourceGraph.Cli -- expand examples/valid/nested-bundle.json
```

This is expected. A diagnostic is part of the result and is not permission to
drop the authored edge. The [architecture guide](../concepts/architecture.md)
explains the bounded expansion contract.

## Build this documentation site

Restore the exact DocFX version and build into `docs/_site`:

```powershell
dotnet tool restore --tool-manifest .config/dotnet-tools.json
dotnet tool run docfx docs/docfx.json
```

The Pages workflow runs the same pinned tool. Generated output is ignored by
Git and is not a source of truth.
