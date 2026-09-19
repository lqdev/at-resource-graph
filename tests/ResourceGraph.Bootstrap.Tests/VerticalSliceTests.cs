using ResourceGraph.Composition;
using ResourceGraph.Core;
using ResourceGraph.Discovery;
using ResourceGraph.Opml;
using ResourceGraph.Syndication;

namespace ResourceGraph.Bootstrap.Tests;

public sealed class CoreModelTests
{
    [Fact]
    public void ReferencesRejectInvalidSchemesAndMembershipPositions()
    {
        Assert.Throws<ArgumentException>(() =>
            new SyndicationReference(new Uri("http://example.test/feed.xml"), SyndicationFormat.Rss));
        Assert.Throws<ArgumentException>(() =>
            new AtRecordReference(new Uri("https://example.test/not-at")));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Membership(-1, new ExplicitDiscoveryReference(new Uri("https://example.test/page"))));
    }

    [Fact]
    public void BundleJsonRoundTripsTypedReferences()
    {
        var bundle = new Bundle(
            "Round trip",
            new[]
            {
                new Membership(
                    0,
                    new SyndicationReference(
                        new Uri("https://example.test/feed.xml"),
                        SyndicationFormat.Rss,
                        "Feed"),
                    "Primary")
            });

        var parsed = BundleJson.Deserialize(BundleJson.Serialize(bundle));

        Assert.Equal(bundle.Name, parsed.Name);
        Assert.Single(parsed.Members);
        Assert.IsType<SyndicationReference>(parsed.Members[0].Resource);
        Assert.Equal("Primary", parsed.Members[0].Label);
    }

    [Fact]
    public void BundleJsonPreservesCanonicalAtUriText()
    {
        var bundle = BundleJson.Deserialize(
            File.ReadAllText(Fixture.RepositoryPath("examples", "valid", "nested-bundle.json")));

        var atRecord = Assert.IsType<AtRecordReference>(bundle.Members[2].Resource);
        var nested = Assert.IsType<NestedBundleReference>(bundle.Members[1].Resource);
        Assert.StartsWith("at://did:plc:", atRecord.AtUri, StringComparison.Ordinal);
        Assert.StartsWith("at://did:plc:", nested.BundleUri, StringComparison.Ordinal);
    }
}

public sealed class SyndicationParserTests
{
    [Fact]
    public void ParsesRssPodcastMediaAndYouTubeMetadata()
    {
        var feed = SyndicationParser.ParseFile(Fixture.Path("syndication", "rss.xml"));

        Assert.Equal(SyndicationFormat.Rss, feed.Format);
        Assert.Single(feed.Items);
        Assert.Equal(2, feed.Items[0].Enclosures.Length);
        Assert.Contains(feed.Items[0].Metadata, item => item.Key.Contains("youtube.com", StringComparison.Ordinal));
        Assert.Contains(feed.Items[0].Metadata, item => item.Key.Contains("itunes.com", StringComparison.Ordinal));
    }

    [Fact]
    public void ParsesAtomAndPodcastFixtures()
    {
        var atom = SyndicationParser.ParseFile(Fixture.Path("syndication", "atom.xml"));
        var podcast = SyndicationParser.ParseFile(Fixture.Path("syndication", "podcast.xml"));

        Assert.Equal(SyndicationFormat.Atom, atom.Format);
        Assert.Single(atom.Items);
        Assert.Single(atom.Items[0].Enclosures);
        Assert.Equal("01:02:03", podcast.Items[0].Enclosures[0].Duration);
    }

    [Fact]
    public void TreatsBlueskyGeneratedRssAsOrdinaryRss()
    {
        var feed = SyndicationParser.ParseFile(Fixture.Path("syndication", "bluesky-rss.xml"));

        Assert.Equal(SyndicationFormat.Rss, feed.Format);
        Assert.Equal("at://did:plc:fixture/app.bsky.feed.post/3fixture", feed.Items[0].Id);
    }

    [Fact]
    public void RejectsMalformedXmlAndConfiguredLimits()
    {
        Assert.Throws<SyndicationParseException>(() =>
            SyndicationParser.ParseFile(Fixture.Path("syndication", "malformed.xml")));
        Assert.Throws<SyndicationLimitExceededException>(() =>
            SyndicationParser.ParseFile(
                Fixture.Path("syndication", "limits.xml"),
                new FeedParseOptions(maxItems: 2)));
        Assert.Throws<SyndicationLimitExceededException>(() =>
            SyndicationParser.ParseFile(
                Fixture.Path("syndication", "rss.xml"),
                new FeedParseOptions(maxBytes: 32)));
    }
}

public sealed class DiscoveryParserTests
{
    [Fact]
    public void ParsesHeadMetadataAndIgnoresBodyScriptsAndOpenGraph()
    {
        var result = HtmlHeadParser.ParseFile(Fixture.Path("discovery", "head.html"));

        Assert.Equal(8, result.Links.Length);
        Assert.Contains(result.Links, link => link.Kind == DiscoveryLinkKind.Rss);
        Assert.Contains(result.Links, link => link.Kind == DiscoveryLinkKind.AtCanonical);
        Assert.DoesNotContain(result.Links, link => link.Uri.AbsoluteUri.Contains("body.xml", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Links, link => link.Uri.AbsoluteUri.Contains("script.xml", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Links, link => link.Uri.AbsoluteUri.Contains("ignored", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsUnsafeDestinations()
    {
        var result = HtmlHeadParser.ParseFile(Fixture.Path("discovery", "unsafe.html"));

        Assert.Single(result.Links);
        Assert.Equal("https://example.test/allowed.xml", result.Links[0].Uri.AbsoluteUri);
        Assert.Equal(2, result.Diagnostics.Count(diagnostic => diagnostic.Code == "discovery.uri.rejected"));
    }

    [Fact]
    public void StopsAtConfiguredHeadByteCap()
    {
        var result = HtmlHeadParser.ParseFile(
            Fixture.Path("discovery", "head.html"),
            new DiscoveryPolicy(maxHeadBytes: 128));

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "discovery.head.limit");
    }

    [Fact]
    public void ReportsMalformedMetadataUrisExplicitly()
    {
        var result = HtmlHeadParser.Parse(
            "<html><head><link rel=\"alternate\" type=\"application/rss+xml\" href=\"not-a-uri\" /></head></html>");

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "discovery.uri.invalid");
        Assert.Empty(result.Links);
    }
}

public sealed class CompositionTests
{
    [Fact]
    public async Task ExpandsNestedBundlesWithProvenanceAndDeduplication()
    {
        const string nestedUri = "at://did:plc:fixture/me.lqdev.resourcegraph.temp.bundle/nested";
        var leaf = new SyndicationReference(new Uri("https://example.test/feed.xml"), SyndicationFormat.Rss);
        var nestedLeaf = new SyndicationReference(
            new Uri("https://example.test/nested-feed.xml"),
            SyndicationFormat.Atom);
        var nested = new Bundle(
            "Nested",
            new[]
            {
                new Membership(0, leaf, "Duplicate feed"),
                new Membership(1, nestedLeaf, "Nested feed")
            },
            selfUriText: nestedUri);
        var root = new Bundle(
            "Root",
            new[]
            {
                new Membership(0, leaf, "Direct feed"),
                new Membership(1, new NestedBundleReference(nestedUri), "Nested")
            });

        var result = await new BundleExpander().ExpandAsync(
            root,
            new InMemoryBundleResolver(new[] { nested }));

        Assert.Equal(2, result.Resources.Length);
        Assert.Contains(result.Resources, resource => resource.Provenance.Path.Length == 2);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "expansion.duplicate");
    }

    [Fact]
    public async Task ReportsCyclesAndDepthLimits()
    {
        const string firstUri = "at://did:plc:fixture/bundle/first";
        const string secondUri = "at://did:plc:fixture/bundle/second";
        var first = new Bundle(
            "First",
            new[] { new Membership(0, new NestedBundleReference(secondUri)) },
            selfUriText: firstUri);
        var second = new Bundle(
            "Second",
            new[] { new Membership(0, new NestedBundleReference(firstUri)) },
            selfUriText: secondUri);

        var result = await new BundleExpander().ExpandAsync(
            first,
            new InMemoryBundleResolver(new[] { first, second }));

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "expansion.cycle");
    }
}

public sealed class OpmlTests
{
    private static readonly int[] ExpectedGraphPositions = [0, 2, 4];

    [Fact]
    public void GraphProfileRoundTripsOrderAndGraphFields()
    {
        var bundle = new Bundle(
            "Graph OPML",
            new[]
            {
                new Membership(
                    4,
                    new AtRecordReference(
                        "at://did:plc:fixture/app.bsky.feed.post/3post",
                        "bafyfixture"),
                    "AT post"),
                new Membership(
                    2,
                    new NestedBundleReference(
                        "at://did:plc:fixture/me.lqdev.resourcegraph.temp.bundle/nested",
                        BundleReferenceMode.Frozen),
                    "Nested bundle"),
                new Membership(
                    0,
                    new SyndicationReference(
                        new Uri("https://example.test/feed.xml"),
                        SyndicationFormat.Atom),
                    "Atom feed")
            });

        var exported = OpmlExporter.Export(bundle, OpmlProfile.Graph);
        var imported = OpmlImporter.Import(exported.Xml, OpmlProfile.Graph);

        Assert.True(imported.IsSuccess);
        Assert.Equal(ExpectedGraphPositions, imported.Bundle!.Members.Select(member => member.Position));
        var atRecord = Assert.IsType<AtRecordReference>(imported.Bundle.Members[2].Resource);
        Assert.Equal("bafyfixture", atRecord.Cid);
        Assert.IsType<NestedBundleReference>(imported.Bundle.Members[1].Resource);
    }

    [Fact]
    public void ConventionalProfileReportsLossAndImportsFeedsAndLinks()
    {
        var importedFixture = OpmlImporter.ImportFile(Fixture.Path("opml", "conventional.opml"));
        var exported = OpmlExporter.Export(importedFixture.Bundle!, OpmlProfile.Conventional);

        Assert.True(importedFixture.IsSuccess);
        Assert.True(exported.LossReport.AuthorshipLost);
        Assert.True(exported.LossReport.MembershipRelationshipsLost);
        Assert.True(exported.LossReport.CidsLost);
        Assert.True(exported.LossReport.NestedBundlesLost);
        Assert.True(exported.LossReport.ProvenanceLost);
        Assert.Equal(2, importedFixture.Bundle!.Members.Length);
    }
}

internal static class Fixture
{
    public static string Root { get; } = FindRepositoryRoot();

    public static string Path(params string[] parts)
    {
        return System.IO.Path.Combine(new[] { Root, "tests", "fixtures" }.Concat(parts).ToArray());
    }

    public static string RepositoryPath(params string[] parts) =>
        System.IO.Path.Combine(new[] { Root }.Concat(parts).ToArray());

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "ResourceGraph.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
