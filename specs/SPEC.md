# AT Resource Graph specification

**Status:** draft incubation. This document is normative only where it uses
"MUST", "MUST NOT", "SHOULD", or "MAY". The temporary namespace and these
prose documents are not a community standard.

## Scope

An AT Resource Graph describes resources, references, and authored membership
edges that can be composed into a bounded feed. RSS and Atom are explicit
inputs. Podcasts, YouTube channel feeds, and Bluesky or other AT-generated RSS
are ordinary syndication inputs; they do not require platform-specific graph
semantics at this layer.

An implementation MAY perform bounded, metadata-only discovery in an HTML
`<head>` when a resource is already known by URL. It MUST NOT scrape page
bodies as part of discovery. Head discovery is optional and MUST be bounded by
the security rules in [SECURITY.md](SECURITY.md).

## Core rules

1. A bundle MUST preserve inline ordered memberships.
2. Removing a membership MUST remove only the authored edge, not the referenced
   external resource.
3. Nested bundles MUST retain the path that introduced them.
4. Expansion MUST be bounded by depth, member count, response bytes, and output
   items.
5. Unknown resource kinds MUST remain inspectable instead of being silently
   discarded.
6. OPML is a projection format. Export MUST state which graph semantics were
   lost.

The wire-oriented draft shape is described in [BUNDLE.md](BUNDLE.md) and
[REFERENCES.md](REFERENCES.md).

The first executable slice implements these rules as follows:

- Core constructors reject invalid schemes, positions, duplicate positions,
  and malformed bundle JSON with explicit exceptions.
- Syndication parsing accepts RSS 2.0 and Atom strings or bounded streams. It
  never fetches an HTML page.
- Discovery scans only `link` and `meta` elements before `</head>` or a byte
  cap. It ignores body content, scripts, JSON-LD, Open Graph, anchors, and
  page content.
- Composition expands nested bundles through an injected resolver and emits
  cycle, limit, duplicate, unavailable-source, and unknown-resource
  diagnostics.
- The CLI exposes offline `validate`, `parse-feed`, `discover-head`,
  `import-opml`, `export-opml`, and `expand` commands.

## Public-read adapters

The public-read adapter boundary is intentionally narrow:

1. `ResourceGraph.AtProto` parses AT record identifiers and decodes public
   `com.atproto.repo.getRecord`-style envelopes. Its HTTP resolver requires an
   injected HTTPS service endpoint, bounded responses, cancellation, and no
   authorization or cookie headers.
2. `ResourceGraph.StandardSite` adapts the existing Discovery result. It does
   not fetch or scrape a document body.
3. `ResourceGraph.Bluesky` decodes only small public post and profile record
   shapes. RSS and Atom references remain ordinary Syndication inputs.

OAuth, authenticated writes, firehose ingestion, PDS deployment, and AppView
projection are outside this slice.

## Incubation and future governance

The initial Lexicons live in `me.lqdev.resourcegraph.temp`. Moving a proven
generic subset to `community.lexicon.*` requires the governance gate in
[GOVERNANCE.md](../GOVERNANCE.md), including public discussion, independent
implementations, conformance fixtures, and community.lexicon approval.
