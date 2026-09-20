# AT Resource Graph packages

These packages are incubating .NET 10 building blocks for composing
protocol-aware resource collections from AT Protocol records, RSS/Atom feeds,
websites, and nested bundles.

## Packages

The first preview publishes these package IDs at version `0.1.0-preview.1`:

- `ResourceGraph.Core`
- `ResourceGraph.AtProto`
- `ResourceGraph.Syndication`
- `ResourceGraph.Discovery`
- `ResourceGraph.StandardSite`
- `ResourceGraph.Bluesky`
- `ResourceGraph.Composition`
- `ResourceGraph.Opml`
- `ResourceGraph.AppView`

The packages target `net10.0`. Package dependencies are emitted from the
project-reference graph, so installing a higher-level package pulls in its
ResourceGraph dependencies at the same version.

## GitHub Packages authentication

GitHub Packages is a private-by-default registry. A local developer needs a
GitHub token with `read:packages` and repository access. With GitHub CLI
authenticated, configure the source once:

```powershell
$env:GITHUB_TOKEN = gh auth token
dotnet nuget add source `
  https://nuget.pkg.github.com/lqdev/index.json `
  --name github `
  --username lqdev `
  --password $env:GITHUB_TOKEN `
  --store-password-in-clear-text
dotnet restore --source https://nuget.pkg.github.com/lqdev/index.json
```

For CI, use the repository's `GITHUB_TOKEN` with `packages: read` and pass the
same source URL without committing credentials.

## Version policy

`0.1.0-preview.1` is the initial immutable preview. The package version can be
overridden for a local build or release with
`-p:ResourceGraphVersion=<version>`. Published NuGet versions cannot be
replaced; publish a new preview or stable version instead.

See the repository's [package publishing guide](https://github.com/lqdev/at-resource-graph/blob/main/docs/getting-started/packages.md)
for the release workflow and AppView sample integration.
