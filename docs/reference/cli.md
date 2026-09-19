# CLI reference

`ResourceGraph.Cli` is an offline executable. Every command reads a local
file, emits deterministic output, and returns a non-zero result for invalid
input or incomplete expansion.

## Commands

| Command | Purpose |
| --- | --- |
| `validate <bundle.json>` | Parse and validate a draft bundle |
| `parse-feed <feed.xml>` | Parse RSS or Atom and print normalized JSON |
| `discover-head <page.html>` | Scan bounded head metadata without body scraping |
| `import-opml <file.opml> [--profile conventional\|graph]` | Convert OPML outlines to a bundle |
| `export-opml <bundle.json> [--profile conventional\|graph] [--output file]` | Write OPML and report loss |
| `expand <bundle.json>` | Expand with the CLI's empty offline resolver |

Run the executable from the repository root:

```powershell
dotnet run --project src/ResourceGraph.Cli -- validate examples/valid/minimal-bundle.json
dotnet run --project src/ResourceGraph.Cli -- discover-head tests/fixtures/discovery/head.html
```

`expand` intentionally reports `source-unavailable` for an unresolved nested
bundle. That result demonstrates the diagnostic contract; it is not a live
network test.

For the implementation entry point, use the source-only
[CLI file on GitHub](https://github.com/lqdev/at-resource-graph/blob/main/src/ResourceGraph.Cli/Program.cs).
GitHub is linked here only for editing and history; the command behavior is
described locally above.
