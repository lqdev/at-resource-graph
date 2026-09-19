using ResourceGraph.Core;
using ResourceGraph.Syndication;

namespace ResourceGraph.Reader;

/// <summary>Offline sample data used by the reference reader.</summary>
public static class SampleReaderData
{
    /// <summary>Gets the deterministic sample bundle.</summary>
    public static Bundle Bundle { get; } = new(
        "Reader sample",
        new[]
        {
            new Membership(
                0,
                new SyndicationReference(
                    new Uri("https://example.test/reader-feed.xml"),
                    SyndicationFormat.Rss,
                    "Reader sample feed"),
                "Sample feed")
        },
        "An offline sample bundle. The reader performs no arbitrary URL fetching.");

    /// <summary>Gets the parsed deterministic sample feed.</summary>
    public static SyndicationFeed Feed { get; } = SyndicationParser.Parse(
        """
        <?xml version="1.0" encoding="utf-8"?>
        <rss version="2.0">
          <channel>
            <title>Reader sample feed</title>
            <link>https://example.test/reader-feed.xml</link>
            <description>Offline reader fixture</description>
            <item>
              <guid>reader-sample-1</guid>
              <title>Offline sample item</title>
              <link>https://example.test/reader-item-1</link>
              <description>This item is bundled with the local reference reader.</description>
              <pubDate>Sat, 19 Sep 2026 14:00:00 GMT</pubDate>
            </item>
          </channel>
        </rss>
        """);
}
