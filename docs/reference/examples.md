# Examples and fixtures

Examples are synthetic and safe to run offline. The source files are copied
into the generated site as downloadable resources.

## Bundle JSON

| Example | Purpose |
| --- | --- |
| [minimal-bundle.json](../../examples/valid/minimal-bundle.json) | One RSS reference |
| [nested-bundle.json](../../examples/valid/nested-bundle.json) | RSS, nested bundle, and AT record in authored order |
| [missing-position.json](../../examples/invalid/missing-position.json) | Intentionally schema-invalid membership |
| [Examples README](../../examples/README.md) | Fixture intent and status |

## Protocol fixtures

| Directory | Coverage |
| --- | --- |
| [`syndication`](../../tests/fixtures/syndication/rss.xml) | RSS, Atom, podcasts, YouTube, limits, and malformed input |
| [`discovery`](../../tests/fixtures/discovery/head.html) | Head metadata and body/script decoys |
| [`opml`](../../tests/fixtures/opml/conventional.opml) | Conventional OPML import |
| [`conformance`](../../tests/fixtures/conformance/at-record.json) | AT, Standard.site, and discovery conformance vectors |

Run the focused test project after changing an adapter:

```powershell
dotnet test tests/ResourceGraph.Bootstrap.Tests/ResourceGraph.Bootstrap.Tests.csproj --no-restore
```
