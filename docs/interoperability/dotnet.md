# .NET interoperability

The .NET libraries target `net10.0`. Use `ResourceGraph.Core` for the wire
format and add protocol projects only when the application needs their
adapter behavior.

## Read and validate a bundle

```csharp
using ResourceGraph.Core;

var json = await File.ReadAllTextAsync("examples/valid/nested-bundle.json");
var bundle = BundleJson.Deserialize(json);
var diagnostics = GraphValidator.Validate(bundle);

Console.WriteLine($"{bundle.Name}: {bundle.Members.Length} members");
foreach (var diagnostic in diagnostics)
{
    Console.WriteLine($"{diagnostic.Severity}: {diagnostic.Code}");
}
```

`BundleJson.Serialize(bundle)` writes deterministic, indented JSON and orders
members by `position`. The constructors reject negative positions, duplicate
positions, invalid schemes, and oversized values.

## Parse a feed

```csharp
using ResourceGraph.Syndication;

var feed = SyndicationParser.ParseFile(
    "tests/fixtures/syndication/atom.xml",
    new FeedParseOptions(maxBytes: 128 * 1024, maxItems: 100));

Console.WriteLine($"{feed.Format}: {feed.Items.Length} items");
```

For a network adapter, fetch bytes under an application policy first and pass a
bounded stream to `SyndicationParser.Parse`. The parser itself never performs
network requests.

## Package boundaries

`ResourceGraph.Core` has no service or web dependencies. `ResourceGraph.Cli`
is an executable rather than an API package. Use the generated
[.NET API reference](../reference/api.md) for public types and the
[CLI reference](../reference/cli.md) for deterministic commands.
