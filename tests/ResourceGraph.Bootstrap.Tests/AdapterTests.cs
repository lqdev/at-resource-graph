using System.Net;
using System.Text;
using System.Text.Json;
using ResourceGraph.AtProto;
using ResourceGraph.Bluesky;
using ResourceGraph.Core;
using ResourceGraph.Discovery;
using ResourceGraph.Reader;
using ResourceGraph.StandardSite;

namespace ResourceGraph.Bootstrap.Tests;

public sealed class AtProtoTests
{
    [Fact]
    public void ParsesAndFormatsColonBearingAtUrisWithOptionalCid()
    {
        var uri = AtUri.Parse(
            "at://did:plc:fixture/app.bsky.feed.post/3fixture?cid=bafyfixturecid");

        Assert.Equal("did:plc:fixture", uri.Did);
        Assert.Equal("app.bsky.feed.post", uri.Collection);
        Assert.Equal("3fixture", uri.Rkey);
        Assert.Equal("bafyfixturecid", uri.Cid);
        Assert.Equal(
            "at://did:plc:fixture/app.bsky.feed.post/3fixture?cid=bafyfixturecid",
            uri.ToString());
        Assert.False(AtUri.TryParse("at://did:plc:fixture/not-a-collection/3rkey", out _));
        Assert.True(AtProtoIdentifierValidation.IsValidHandle("example.test"));
        Assert.False(AtProtoIdentifierValidation.IsValidHandle("not a handle"));
    }

    [Fact]
    public void DecodesGetRecordEnvelope()
    {
        var json = File.ReadAllText(Fixture.Path("conformance", "at-record.json"));
        var envelope = AtRecordEnvelopeDecoder.Decode(json);

        Assert.Equal("app.bsky.feed.post", envelope.RecordType);
        Assert.Equal("bafyfixturecid", envelope.Cid);
        Assert.Equal("A public fixture post.", envelope.Value.GetProperty("text").GetString());
    }

    [Fact]
    public async Task HttpResolverUsesPublicHttpsReadAndReturnsDiagnostics()
    {
        var transport = new RecordingTransport(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    File.ReadAllText(Fixture.Path("conformance", "at-record.json")),
                    Encoding.UTF8,
                    "application/json")
            });
        using var resolver = HttpAtRecordResolver.CreateForTesting(
            new Uri("https://pds.example.test/xrpc/com.atproto.repo.getRecord"),
            transport,
            new AtRecordResolverOptions(addressResolver: new StaticAddressResolver(
                IPAddress.Parse("93.184.216.34"))));

        var result = await resolver.ResolveAsync(
            AtUri.Parse("at://did:plc:fixture/app.bsky.feed.post/3fixture"));

        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var request = transport.Request!;
        Assert.Equal("did:plc:fixture", request.GetQueryParameter("repo"));
        Assert.Null(request.Headers.Authorization);
        Assert.False(request.Headers.Contains("Cookie"));
    }

    [Fact]
    public void RejectsCredentialedClientsAndInsecureEndpoints()
    {
        using var unsafeInner = new HttpClientHandler();
        using var wrapped = new UnsafeDelegatingHandler(unsafeInner);
#pragma warning disable RG0001
        Assert.Throws<ArgumentException>(() =>
            new HttpAtRecordResolver(
                wrapped,
                new Uri("https://pds.example.test/xrpc/com.atproto.repo.getRecord")));
#pragma warning restore RG0001
        Assert.Throws<ArgumentException>(() =>
            HttpAtRecordResolver.CreateForTesting(
                new Uri("http://pds.example.test/xrpc/com.atproto.repo.getRecord"),
                new RecordingTransport(_ => new HttpResponseMessage(HttpStatusCode.OK))));
        Assert.Throws<ArgumentException>(() =>
            HttpAtRecordResolver.CreateForTesting(
                new Uri("https://127.0.0.1/xrpc/com.atproto.repo.getRecord"),
                new RecordingTransport(_ => new HttpResponseMessage(HttpStatusCode.OK))));
        Assert.Throws<ArgumentException>(() =>
            HttpAtRecordResolver.CreateForTesting(
                new Uri("https://[::ffff:10.0.0.1]/xrpc/com.atproto.repo.getRecord"),
                new RecordingTransport(_ => new HttpResponseMessage(HttpStatusCode.OK))));
        Assert.Throws<ArgumentException>(() =>
            HttpAtRecordResolver.CreateForTesting(
                new Uri("https://metadata.google.internal/xrpc/com.atproto.repo.getRecord"),
                new RecordingTransport(_ => new HttpResponseMessage(HttpStatusCode.OK))));
        Assert.Throws<ArgumentException>(() =>
            HttpAtRecordResolver.CreateForTesting(
                new Uri("https://pds.example.test:8443/xrpc/com.atproto.repo.getRecord"),
                new RecordingTransport(_ => new HttpResponseMessage(HttpStatusCode.OK))));
    }

    [Fact]
    public async Task ReturnsExplicitResponseSizeDiagnostics()
    {
        var transport = new RecordingTransport(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(new string('x', 128), Encoding.UTF8, "application/json")
            });
        using var resolver = HttpAtRecordResolver.CreateForTesting(
            new Uri("https://pds.example.test/xrpc/com.atproto.repo.getRecord"),
            transport,
            new AtRecordResolverOptions(
                maxResponseBytes: 16,
                addressResolver: new StaticAddressResolver(IPAddress.Parse("93.184.216.34"))));

        var result = await resolver.ResolveAsync(
            AtUri.Parse("at://did:plc:fixture/app.bsky.feed.post/3fixture"));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "atproto.response.limit");
    }

    [Fact]
    public async Task RejectsRedirectToPrivateDestinationWithoutSecondRequest()
    {
        var transport = new RecordingTransport(
            _ => new HttpResponseMessage(HttpStatusCode.Found)
            {
                Headers = { Location = new Uri("https://127.0.0.1/private") }
            });
        using var resolver = HttpAtRecordResolver.CreateForTesting(
            new Uri("https://pds.example.test/xrpc/com.atproto.repo.getRecord"),
            transport,
            new AtRecordResolverOptions(addressResolver: new StaticAddressResolver(
                IPAddress.Parse("93.184.216.34"))));

        var result = await resolver.ResolveAsync(
            AtUri.Parse("at://did:plc:fixture/app.bsky.feed.post/3fixture"));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "atproto.redirect.rejected");
        Assert.Equal(1, transport.RequestCount);
    }

    [Fact]
    public async Task RejectsRedirectToHttpWithoutSecondRequest()
    {
        var transport = new RecordingTransport(
            _ => new HttpResponseMessage(HttpStatusCode.MovedPermanently)
            {
                Headers = { Location = new Uri("http://example.test/insecure") }
            });
        using var resolver = HttpAtRecordResolver.CreateForTesting(
            new Uri("https://pds.example.test/xrpc/com.atproto.repo.getRecord"),
            transport,
            new AtRecordResolverOptions(addressResolver: new StaticAddressResolver(
                IPAddress.Parse("93.184.216.34"))));

        var result = await resolver.ResolveAsync(
            AtUri.Parse("at://did:plc:fixture/app.bsky.feed.post/3fixture"));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "atproto.redirect.rejected");
        Assert.Equal(1, transport.RequestCount);
    }

    [Fact]
    public async Task ValidatedAddressSetRejectsDnsRebindingAndUntrustedProxyDestination()
    {
        var resolver = new SequenceAddressResolver(
            [IPAddress.Parse("93.184.216.34")],
            [IPAddress.Parse("10.0.0.1")]);
        var policy = new AtEndpointPolicy(addressResolver: resolver);
        var endpoint = new Uri("https://pds.example.test/xrpc/com.atproto.repo.getRecord");

        Assert.Null(await policy.ValidateAsync(endpoint));
        var rebound = await policy.ResolveValidatedAddressesAsync("pds.example.test", 443);

        Assert.False(rebound.IsSuccess);
        Assert.Contains(
            rebound.Diagnostics,
            diagnostic => diagnostic.Code == "atproto.endpoint.rejected");
    }

    [Fact]
    public void ProductionTransportDisablesProxyAndHandlerSideEffects()
    {
        using var handler = SecureAtRecordTransport.CreateHandler(
            new AtEndpointPolicy(addressResolver: new StaticAddressResolver(
                IPAddress.Parse("93.184.216.34"))));

        Assert.False(handler.UseProxy);
        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseCookies);
        Assert.Null(handler.Credentials);
        Assert.False(handler.PreAuthenticate);
        Assert.NotNull(handler.ConnectCallback);
    }

    private sealed class RecordingTransport : IAtRecordTransport
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;

        public RecordingTransport(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
            this.responder = responder;

        public HttpRequestMessage? Request { get; private set; }
        public int RequestCount { get; private set; }

        public ValueTask<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            RequestCount++;
            var response = responder(request);
            response.RequestMessage = request;
            return ValueTask.FromResult(response);
        }

        public void Dispose()
        {
        }
    }

    private sealed class StaticAddressResolver : IAtEndpointAddressResolver
    {
        private readonly IReadOnlyList<IPAddress> addresses;

        public StaticAddressResolver(params IPAddress[] addresses) =>
            this.addresses = addresses;

        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(
            string host,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(addresses);
    }

    private sealed class SequenceAddressResolver : IAtEndpointAddressResolver
    {
        private readonly IReadOnlyList<IPAddress>[] sequences;
        private int callCount;

        public SequenceAddressResolver(params IReadOnlyList<IPAddress>[] sequences) =>
            this.sequences = sequences;

        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(
            string host,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(
                sequences[Math.Min(callCount++, sequences.Length - 1)]);
    }

    private sealed class UnsafeDelegatingHandler : DelegatingHandler
    {
        public UnsafeDelegatingHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }
    }
}

public sealed class StandardSiteAndBlueskyTests
{
    [Fact]
    public void AdaptsStandardSiteMetadataWithoutBodyScraping()
    {
        var discovery = HtmlHeadParser.ParseFile(Fixture.Path("conformance", "standard-site-head.html"));
        var result = StandardSiteMetadataAdapter.FromDiscovery(discovery);

        Assert.True(result.IsSuccess);
        Assert.Equal("site.standard.document", result.Value!.Document.Collection);
        Assert.Equal("site.standard.publication", result.Value.Publication!.Collection);
        Assert.Equal("app.bsky.actor.profile", result.Value.Author!.Collection);
    }

    [Fact]
    public void ProjectsPublicBlueskyPostsAndLeavesSyndicationOnItsPath()
    {
        var envelope = AtRecordEnvelopeDecoder.Decode(
            File.ReadAllText(Fixture.Path("conformance", "at-record.json")));
        var projected = BlueskyAdapter.Project(envelope);
        var feed = ResourceGraph.Syndication.SyndicationParser.ParseFile(
            Fixture.Path("syndication", "bluesky-rss.xml"));

        Assert.True(projected.IsSuccess);
        Assert.Equal(BlueskyItemKind.Post, projected.Value!.Kind);
        Assert.Equal("did:plc:fixture", projected.Value.AuthorDid);
        Assert.Same(feed, BlueskyAdapter.KeepSyndicationPath(feed));
        Assert.Equal(
            BlueskyReferenceKind.Syndication,
            BlueskyAdapter.Classify(new SyndicationReference(
                new Uri("https://example.test/at-feed.xml"),
                SyndicationFormat.Rss)));
    }
}

public sealed class ReaderTests
{
    [Fact]
    public void OfflineReaderSamplesAreBoundedAndNoFetchIsNeeded()
    {
        Assert.Single(SampleReaderData.Bundle.Members);
        Assert.Single(SampleReaderData.Feed.Items);
        Assert.DoesNotContain(
            SampleReaderData.Bundle.Members,
            member => member.Resource is ExplicitDiscoveryReference);
    }
}

internal static class HttpRequestMessageExtensions
{
    public static string? GetQueryParameter(this HttpRequestMessage request, string name)
    {
        var query = request.RequestUri?.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries) ?? [];
        var pair = query.FirstOrDefault(item => item.StartsWith(name + "=", StringComparison.Ordinal));
        return pair is null ? null : Uri.UnescapeDataString(pair[(name.Length + 1)..]);
    }
}
