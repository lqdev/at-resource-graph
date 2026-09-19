# Pinned tools

The Pages workflow restores DocFX from `.config/dotnet-tools.json` at exact
version `2.80.1`. The workflow also pins every GitHub Action to an immutable
commit SHA and keeps the release tag in a comment for review. The build job
has only `contents: read`; the deploy job alone receives `pages: write` and
`id-token: write`.
