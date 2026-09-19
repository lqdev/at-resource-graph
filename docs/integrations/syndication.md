# RSS and Atom integration

RSS 2.0 and Atom are first-class source formats. The parser normalizes both
formats to `SyndicationFeed` and `SyndicationItem`; the graph does not require
platform-specific semantics for podcasts, YouTube feeds, or Bluesky-generated
RSS.

## Normalized output

A feed contains a format, optional ID/title/link/description, items in source
order, non-fatal diagnostics, and preserved namespace-qualified metadata. An
item contains an ID, title, link, summary/content, publication and update
times, enclosures, and preserved metadata.

Namespace media and podcast enclosures are represented as
`SyndicationEnclosure` values. Invalid dates and absolute URI fields become
diagnostics where possible; malformed XML remains a parse error.

## Bounded parsing

Parsing is offline and does not fetch an HTML page. Default limits are:

| Limit | Default |
| --- | ---: |
| UTF-8 source bytes | 2 MiB |
| Items | 1,000 |
| XML element depth | 64 |

Callers can provide `FeedParseOptions` with tighter limits. DTD processing and
external XML resolution are disabled. See the generated
[Syndication API](../reference/api.md) and the
[security constraints](../operations/security.md).

## Try it

```powershell
dotnet run --project src/ResourceGraph.Cli -- parse-feed tests/fixtures/syndication/rss.xml
dotnet run --project src/ResourceGraph.Cli -- parse-feed tests/fixtures/syndication/podcast.xml
dotnet run --project src/ResourceGraph.Cli -- parse-feed tests/fixtures/syndication/youtube.xml
```

The local fixtures are synthetic and can be downloaded from the
[examples and fixtures reference](../reference/examples.md). A feed reference
in a bundle is only an identity; item normalization happens when an adapter is
invoked.

For normative scope and unknown-resource behavior, read the local
[specification](../../specs/SPEC.md).
