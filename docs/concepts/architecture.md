# Architecture

AT Resource Graph separates authored graph structure from resource adapters.
The separation lets a consumer preserve a collection even when it cannot
resolve every source.

## Data flow

```text
bundle JSON or AT record
        |
        v
  Core validation -----> ordered memberships and stable identities
        |
        +--> syndication parser ----> normalized RSS/Atom items
        +--> AT envelope decoder ---> public record value + CID
        +--> head discovery --------> bounded metadata references
        +--> bundle expander --------> resources + provenance + diagnostics
        +--> OPML projection --------> conventional or graph profile
```

`ResourceGraph.Core` owns the graph model and does not depend on a service,
database, web framework, UI, or AT SDK. Adapters depend on Core and add one
protocol boundary at a time.

| Project | Responsibility |
| --- | --- |
| `ResourceGraph.Core` | Bundles, memberships, references, JSON, diagnostics |
| `ResourceGraph.Composition` | Bounded nested expansion and provenance |
| `ResourceGraph.Syndication` | RSS/Atom parsing and normalized feed items |
| `ResourceGraph.AtProto` | AT URI parsing, public envelopes, safe endpoint policy |
| `ResourceGraph.Discovery` | Bounded HTML head metadata discovery |
| `ResourceGraph.StandardSite` | Standard.site metadata projection |
| `ResourceGraph.Bluesky` | Small public post/profile projections |
| `ResourceGraph.Opml` | Conventional and graph OPML profiles |
| `ResourceGraph.Cli` | Deterministic offline command surface |
| `ResourceGraph.Reader` | Local reference reader; no arbitrary URL fetching |

Generated links to the public types are collected in the [.NET API reference](../reference/api.md).

## Graph invariants

1. A bundle owns inline membership edges. Removing an edge does not delete the
   referenced resource.
2. `position` and array order together express authored order. Positions must
   be non-negative and unique.
3. Nested expansion retains the complete membership path.
4. Unknown resource kinds remain inspectable.
5. Limits and failures produce diagnostics. They do not silently erase edges.
6. OPML is a projection, not the canonical graph format.

The normative version of these rules is the local
[AT Resource Graph specification](../../specs/SPEC.md). Read
[the bundle model](../../specs/BUNDLE.md) and
[the reference model](../../specs/REFERENCES.md) for field-level requirements.
