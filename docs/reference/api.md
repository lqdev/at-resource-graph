# .NET API reference

DocFX generates API pages from the `.csproj` files under `src/` during the
documentation build. The links below are stable conceptual entry points; use
the namespace and type search on a generated site for members.

## Public libraries

| Library | API surface |
| --- | --- |
| Core | [ResourceGraph.Core](xref:ResourceGraph.Core) |
| Composition | [ResourceGraph.Composition](xref:ResourceGraph.Composition) |
| Syndication | [ResourceGraph.Syndication](xref:ResourceGraph.Syndication) |
| AT Protocol | [ResourceGraph.AtProto](xref:ResourceGraph.AtProto) |
| Discovery | [ResourceGraph.Discovery](xref:ResourceGraph.Discovery) |
| Standard.site | [ResourceGraph.StandardSite](xref:ResourceGraph.StandardSite) |
| Bluesky | [ResourceGraph.Bluesky](xref:ResourceGraph.Bluesky) |
| OPML | [ResourceGraph.Opml](xref:ResourceGraph.Opml) |

The executable projects (`ResourceGraph.Cli`, `ResourceGraph.Reader`) are
documented by their local [CLI guide](cli.md) and
[security boundary](../operations/security.md). Small placeholder projects
without public types may not produce a useful API page.

If a generated link is absent in a particular build, use DocFX search for a
namespace or type. The source is the authority for implementation details;
the generated pages are the maintained public reference.
