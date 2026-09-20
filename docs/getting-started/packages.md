# ResourceGraph packages

The reusable libraries are published as `net10.0` packages to the lqdev GitHub
Packages NuGet registry. The repository publishes only these projects:

| Package ID | Project | Role |
| --- | --- | --- |
| `ResourceGraph.Core` | `src/ResourceGraph.Core` | Protocol-independent models and validation |
| `ResourceGraph.AtProto` | `src/ResourceGraph.AtProto` | AT Protocol identifiers and public reads |
| `ResourceGraph.Syndication` | `src/ResourceGraph.Syndication` | RSS and Atom normalization |
| `ResourceGraph.Discovery` | `src/ResourceGraph.Discovery` | Safe HTML metadata discovery |
| `ResourceGraph.StandardSite` | `src/ResourceGraph.StandardSite` | Standard.site metadata adaptation |
| `ResourceGraph.Bluesky` | `src/ResourceGraph.Bluesky` | Bluesky post and profile projections |
| `ResourceGraph.Composition` | `src/ResourceGraph.Composition` | Bounded nested-bundle expansion |
| `ResourceGraph.Opml` | `src/ResourceGraph.Opml` | OPML import and export |
| `ResourceGraph.AppView` | `src/ResourceGraph.AppView` | Transport-neutral AppView contracts |

`ResourceGraph.Presentation` is currently an empty project and is not
published. `ResourceGraph.Aspire` is a future topology placeholder.
`ResourceGraph.Cli`, `ResourceGraph.Reader`, and `ResourceGraph.Docs` are
applications or documentation tooling, not reusable library packages. The test
project is never packed.

## Consume the packages locally

GitHub Packages requires authentication even for restore. With GitHub CLI
authenticated and a token that has `read:packages` plus repository access:

```powershell
$env:GITHUB_TOKEN = gh auth token
dotnet nuget add source `
  https://nuget.pkg.github.com/lqdev/index.json `
  --name github `
  --username lqdev `
  --password $env:GITHUB_TOKEN `
  --store-password-in-clear-text
```

Add the packages needed by the AppView sample. `ResourceGraph.AppView` brings
in `ResourceGraph.Core`; add additional adapters explicitly when the sample
uses them:

```powershell
dotnet add package ResourceGraph.AppView `
  --version 0.1.0-preview.1 `
  --source https://nuget.pkg.github.com/lqdev/index.json
dotnet add package ResourceGraph.Bluesky `
  --version 0.1.0-preview.1 `
  --source https://nuget.pkg.github.com/lqdev/index.json
```

Replace local project references to the primitives with these
`PackageReference` entries; do not copy source files or generated site output
into the sample. Keep all ResourceGraph package references on the same preview
version so their generated dependencies resolve consistently.

## Publish a preview

The `Publish ResourceGraph packages` workflow runs restore, Release build,
tests, and packing before pushing the approved package list to
`https://nuget.pkg.github.com/lqdev/index.json` with the workflow's
least-privilege `GITHUB_TOKEN`. Run it manually with a version input, or push a
tag named `resourcegraph-v<version>`, for example:

```powershell
git tag resourcegraph-v0.1.0-preview.1
git push origin resourcegraph-v0.1.0-preview.1
```

The workflow rejects invalid versions and the registry rejects a second
publication of an existing version. Package versions are immutable, so use a
new preview number for every subsequent publication.
