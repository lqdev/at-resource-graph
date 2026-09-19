using System.Collections.Immutable;
using System.ComponentModel;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ResourceGraph.Core;

namespace ResourceGraph.AtProto;

/// <summary>A decoded response from com.atproto.repo.getRecord.</summary>
public sealed class AtRecordEnvelope
{
    /// <summary>Creates an immutable record envelope.</summary>
    public AtRecordEnvelope(AtUri uri, string cid, JsonElement value)
    {
        Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        Cid = AtProtoIdentifierValidation.ValidateCid(cid);
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("The AT record value must be a JSON object.", nameof(value));
        }

        Value = value.Clone();
        RecordType = value.TryGetProperty("$type", out var type) && type.ValueKind == JsonValueKind.String
            ? type.GetString()
            : null;
    }

    /// <summary>Gets the AT URI.</summary>
    public AtUri Uri { get; }

    /// <summary>Gets the response CID.</summary>
    public string Cid { get; }

    /// <summary>Gets the decoded record object.</summary>
    public JsonElement Value { get; }

    /// <summary>Gets the optional Lexicon type discriminator.</summary>
    public string? RecordType { get; }
}

/// <summary>Decodes public getRecord response JSON.</summary>
public static class AtRecordEnvelopeDecoder
{
    /// <summary>Decodes a JSON response and validates its URI, CID, and record object.</summary>
    public static AtRecordEnvelope Decode(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("A getRecord response is required.", nameof(json));
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The getRecord response must be a JSON object.");
        }

        var uri = RequiredString(root, "uri");
        var cid = RequiredString(root, "cid");
        if (!root.TryGetProperty("value", out var value))
        {
            throw new JsonException("The getRecord response requires a value object.");
        }

        return new AtRecordEnvelope(AtUri.Parse(uri), cid, value);
    }

    private static string RequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new JsonException($"The getRecord response requires a non-empty '{propertyName}'.");
        }

        return property.GetString()!;
    }
}

/// <summary>Resolves DNS addresses for endpoint safety checks.</summary>
public interface IAtEndpointAddressResolver
{
    /// <summary>Resolves all addresses for a host without opening an HTTP connection.</summary>
    ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(
        string host,
        CancellationToken cancellationToken = default);
}

/// <summary>Uses the operating system DNS resolver for endpoint validation.</summary>
public sealed class SystemAtEndpointAddressResolver : IAtEndpointAddressResolver
{
    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(
        string host,
        CancellationToken cancellationToken = default) =>
        await Dns.GetHostAddressesAsync(host, cancellationToken);
}

/// <summary>Policy that rejects unsafe HTTP service destinations.</summary>
public sealed class AtEndpointPolicy
{
    private static readonly string[] UnsafeHostNames =
    [
        "localhost",
        "metadata",
        "metadata.google.internal",
        "metadata.azure.internal",
        "instance-data.ec2.internal"
    ];

    private readonly IAtEndpointAddressResolver addressResolver;
    private readonly ImmutableHashSet<int> allowedPorts;

    /// <summary>Creates an endpoint policy.</summary>
    public AtEndpointPolicy(
        IEnumerable<int>? allowedPorts = null,
        IAtEndpointAddressResolver? addressResolver = null)
    {
        var ports = (allowedPorts ?? [443]).ToImmutableHashSet();
        if (ports.Count == 0 || ports.Any(port => port is < 1 or > 65535))
        {
            throw new ArgumentException(
                "At least one valid TCP port must be allowed.",
                nameof(allowedPorts));
        }

        this.allowedPorts = ports;
        this.addressResolver = addressResolver ?? new SystemAtEndpointAddressResolver();
    }

    /// <summary>Validates scheme, authority, port, host names, and all resolved addresses.</summary>
    public async ValueTask<GraphDiagnostic?> ValidateAsync(
        Uri endpoint,
        bool allowQuery = false,
        CancellationToken cancellationToken = default)
    {
        var syntacticDiagnostic = ValidateSyntactic(endpoint, allowQuery);
        if (syntacticDiagnostic is not null)
        {
            return syntacticDiagnostic;
        }

        var addresses = await ResolveValidatedAddressesAsync(
            endpoint.DnsSafeHost,
            endpoint.Port,
            cancellationToken);
        return addresses.IsSuccess
            ? null
            : addresses.Diagnostics.FirstOrDefault();
    }

    /// <summary>
    /// Resolves and validates the exact address set that the production socket
    /// callback is allowed to contact.
    /// </summary>
    public async ValueTask<OperationResult<ImmutableArray<IPAddress>>> ResolveValidatedAddressesAsync(
        string host,
        int port,
        CancellationToken cancellationToken = default)
    {
        var hostDiagnostic = ValidateHostAndPort(host, port);
        if (hostDiagnostic is not null)
        {
            return new OperationResult<ImmutableArray<IPAddress>>(default, [hostDiagnostic]);
        }

        var normalizedHost = host.TrimEnd('.').ToLowerInvariant();
        if (IPAddress.TryParse(normalizedHost, out var literalAddress))
        {
            return IsUnsafeAddress(literalAddress)
                ? new OperationResult<ImmutableArray<IPAddress>>(
                    default,
                    [Rejected(
                        $"The service endpoint address '{normalizedHost}' is private, local, reserved, or otherwise unsafe.",
                        normalizedHost)])
                : new OperationResult<ImmutableArray<IPAddress>>([literalAddress]);
        }

        IReadOnlyList<IPAddress> addresses;
        try
        {
            addresses = await addressResolver.ResolveAsync(normalizedHost, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new OperationResult<ImmutableArray<IPAddress>>(
                default,
                [Rejected(
                    $"DNS resolution timed out for service endpoint host '{normalizedHost}'.",
                    normalizedHost,
                    "atproto.endpoint.dns-timeout")]);
        }
        catch (Exception exception) when (exception is SocketException or InvalidOperationException)
        {
            return new OperationResult<ImmutableArray<IPAddress>>(
                default,
                [Rejected(
                    $"DNS resolution failed for service endpoint host '{normalizedHost}': {exception.Message}",
                    normalizedHost,
                    "atproto.endpoint.dns-failure")]);
        }

        var validatedAddresses = addresses
            .Where(address => !IsUnsafeAddress(address))
            .Distinct()
            .ToImmutableArray();
        if (addresses.Count == 0 || addresses.Any(IsUnsafeAddress))
        {
            return new OperationResult<ImmutableArray<IPAddress>>(
                default,
                [Rejected(
                    $"The service endpoint host '{normalizedHost}' resolves to an unsafe destination.",
                    normalizedHost)]);
        }

        return new OperationResult<ImmutableArray<IPAddress>>(validatedAddresses);
    }

    /// <summary>Validates endpoint properties that do not require DNS resolution.</summary>
    public GraphDiagnostic? ValidateSyntactic(Uri endpoint, bool allowQuery = false)
    {
        if (endpoint is null ||
            !endpoint.IsAbsoluteUri ||
            !endpoint.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return Rejected("The service endpoint must use HTTPS.", endpoint?.AbsoluteUri);
        }

        if (!string.IsNullOrEmpty(endpoint.UserInfo) ||
            (!allowQuery && !string.IsNullOrEmpty(endpoint.Query)) ||
            !string.IsNullOrEmpty(endpoint.Fragment) ||
            string.IsNullOrWhiteSpace(endpoint.Host))
        {
            return Rejected(
                "The service endpoint must not contain user information, query, fragment, or an empty host.",
                endpoint.AbsoluteUri);
        }

        return ValidateHostAndPort(endpoint.DnsSafeHost, endpoint.Port);
    }

    /// <summary>Returns whether a hostname or address is known to be local or metadata-only.</summary>
    public static bool IsUnsafeHostName(string host)
    {
        var normalized = host.TrimEnd('.').ToLowerInvariant();
        return UnsafeHostNames.Contains(normalized, StringComparer.Ordinal) ||
            normalized.EndsWith(".localhost", StringComparison.Ordinal) ||
            normalized.EndsWith(".local", StringComparison.Ordinal) ||
            normalized.EndsWith(".internal", StringComparison.Ordinal);
    }

    /// <summary>Returns whether an IP address is local, private, multicast, metadata, or reserved.</summary>
    public static bool IsUnsafeAddress(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.IPv6Loopback) ||
            address.IsIPv6LinkLocal ||
            address.IsIPv6SiteLocal)
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        if (bytes.Length == 16)
        {
            return (bytes[0] & 0xfe) == 0xfc ||
                bytes[0] == 0xff ||
                IsIpv6Documentation(bytes);
        }

        if (bytes.Length != 4)
        {
            return true;
        }

        var first = bytes[0];
        var second = bytes[1];
        var third = bytes[2];
        return first == 0 ||
            first == 10 ||
            first == 127 ||
            (first == 100 && second is >= 64 and <= 127) ||
            (first == 169 && second == 254) ||
            (first == 172 && second is >= 16 and <= 31) ||
            (first == 192 && second == 168) ||
            (first == 192 && second == 0 && third == 0) ||
            (first == 192 && second == 0 && third == 2) ||
            (first == 198 && second is 18 or 19) ||
            (first == 198 && second == 51) ||
            (first == 203 && second == 0 && third == 113) ||
            first >= 224;
    }

    private static bool IsIpv6Documentation(byte[] bytes) =>
        bytes[0] == 0x20 &&
        bytes[1] == 0x01 &&
        bytes[2] == 0x0d &&
        bytes[3] == 0xb8;

    private GraphDiagnostic? ValidateHostAndPort(string host, int port)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return Rejected("The service endpoint host is required.", host);
        }

        if (!allowedPorts.Contains(port))
        {
            return Rejected(
                $"The service endpoint port {port} is not in the configured allowlist.",
                host);
        }

        var normalizedHost = host.TrimEnd('.').ToLowerInvariant();
        if (IsUnsafeHostName(normalizedHost))
        {
            return Rejected(
                $"The service endpoint host '{normalizedHost}' is reserved or local.",
                normalizedHost);
        }

        return IPAddress.TryParse(normalizedHost, out var literalAddress) && IsUnsafeAddress(literalAddress)
            ? Rejected(
                $"The service endpoint address '{normalizedHost}' is private, local, reserved, or otherwise unsafe.",
                normalizedHost)
            : null;
    }

    private static GraphDiagnostic Rejected(
        string message,
        string? identity,
        string code = "atproto.endpoint.rejected") =>
        new(code, message, DiagnosticSeverity.Error, identity);
}

/// <summary>Public-read limits for an AT record HTTP request.</summary>
public sealed class AtRecordResolverOptions
{
    /// <summary>Creates validated resolver limits and endpoint policy dependencies.</summary>
    public AtRecordResolverOptions(
        int maxResponseBytes = 512 * 1024,
        TimeSpan? timeout = null,
        IEnumerable<int>? allowedPorts = null,
        IAtEndpointAddressResolver? addressResolver = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResponseBytes);
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(10);
        if (effectiveTimeout <= TimeSpan.Zero || effectiveTimeout > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        MaxResponseBytes = maxResponseBytes;
        Timeout = effectiveTimeout;
        EndpointPolicy = new AtEndpointPolicy(allowedPorts, addressResolver);
    }

    /// <summary>Gets the maximum response body size.</summary>
    public int MaxResponseBytes { get; }

    /// <summary>Gets the request timeout.</summary>
    public TimeSpan Timeout { get; }

    /// <summary>Gets the endpoint safety policy.</summary>
    public AtEndpointPolicy EndpointPolicy { get; }
}

/// <summary>Resolves public AT records without authenticated writes.</summary>
public interface IAtRecordResolver
{
    /// <summary>Gets one public record envelope.</summary>
    ValueTask<OperationResult<AtRecordEnvelope>> ResolveAsync(
        AtUri uri,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Explicit test transport boundary. Production construction does not accept
/// arbitrary handlers or this interface.
/// </summary>
public interface IAtRecordTransport : IDisposable
{
    /// <summary>Sends one already-validated public-read request.</summary>
    ValueTask<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken);
}

/// <summary>Resolves getRecord responses through a controlled no-proxy transport.</summary>
public sealed class HttpAtRecordResolver : IAtRecordResolver, IDisposable
{
    private readonly IAtRecordTransport transport;
    private readonly Uri serviceEndpoint;
    private readonly AtRecordResolverOptions options;

    /// <summary>
    /// Creates the production resolver. The transport is constructed internally
    /// with no proxy, redirects, cookies, credentials, or pre-authentication.
    /// </summary>
    public HttpAtRecordResolver(
        Uri serviceEndpoint,
        AtRecordResolverOptions? options = null)
    {
        serviceEndpoint = serviceEndpoint ?? throw new ArgumentNullException(nameof(serviceEndpoint));
        this.options = options ?? new AtRecordResolverOptions();
        ValidateEndpointShape(serviceEndpoint);
        var diagnostic = this.options.EndpointPolicy.ValidateSyntactic(serviceEndpoint);
        if (diagnostic is not null)
        {
            throw new ArgumentException(diagnostic.Message, nameof(serviceEndpoint));
        }

        this.serviceEndpoint = serviceEndpoint;
        transport = SecureAtRecordTransport.Create(this.options.EndpointPolicy);
    }

    /// <summary>
    /// Creates a resolver over an explicit offline test transport. This path is
    /// separate from production construction and never creates an HTTP handler.
    /// </summary>
    public static HttpAtRecordResolver CreateForTesting(
        Uri serviceEndpoint,
        IAtRecordTransport transport,
        AtRecordResolverOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        return new HttpAtRecordResolver(serviceEndpoint, options ?? new AtRecordResolverOptions(), transport);
    }

    /// <summary>
    /// Fails closed for compatibility with callers that previously supplied
    /// arbitrary handler chains. Such chains cannot be inspected safely.
    /// </summary>
    [Obsolete(
        "Arbitrary HttpMessageHandler instances are rejected. Use the production constructor or CreateForTesting.",
        DiagnosticId = "RG0001")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public HttpAtRecordResolver(
        HttpMessageHandler handler,
        Uri serviceEndpoint,
        AtRecordResolverOptions? options = null)
    {
        throw new ArgumentException(
            "Arbitrary HttpMessageHandler instances are rejected because handler chains cannot be inspected safely.",
            nameof(handler));
    }

    private HttpAtRecordResolver(
        Uri serviceEndpoint,
        AtRecordResolverOptions options,
        IAtRecordTransport transport)
    {
        ValidateEndpointShape(serviceEndpoint);
        var diagnostic = options.EndpointPolicy.ValidateSyntactic(serviceEndpoint);
        if (diagnostic is not null)
        {
            throw new ArgumentException(diagnostic.Message, nameof(serviceEndpoint));
        }

        this.serviceEndpoint = serviceEndpoint;
        this.options = options;
        this.transport = transport;
    }

    /// <inheritdoc />
    public async ValueTask<OperationResult<AtRecordEnvelope>> ResolveAsync(
        AtUri uri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        var endpointDiagnostic = await options.EndpointPolicy.ValidateAsync(
            serviceEndpoint,
            cancellationToken: cancellationToken);
        if (endpointDiagnostic is not null)
        {
            return Failure(endpointDiagnostic);
        }

        var requestUri = BuildRequestUri(uri);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(options.Timeout);
        try
        {
            using var response = await transport.SendAsync(request, timeoutCancellation.Token);
            if ((int)response.StatusCode is >= 300 and <= 399)
            {
                return Failure(new GraphDiagnostic(
                    "atproto.redirect.rejected",
                    "Redirects are disabled for public AT record reads; the Location target was not contacted.",
                    DiagnosticSeverity.Error,
                    response.Headers.Location?.ToString() ?? requestUri.AbsoluteUri));
            }

            var finalUri = response.RequestMessage?.RequestUri ?? requestUri;
            var finalEndpointDiagnostic = await options.EndpointPolicy.ValidateAsync(
                finalUri,
                allowQuery: true,
                cancellationToken: timeoutCancellation.Token);
            if (finalEndpointDiagnostic is not null)
            {
                return Failure(new GraphDiagnostic(
                    "atproto.final-endpoint.rejected",
                    $"The final response endpoint was rejected: {finalEndpointDiagnostic.Message}",
                    DiagnosticSeverity.Error,
                    finalEndpointDiagnostic.ResourceIdentity));
            }

            if (!response.IsSuccessStatusCode)
            {
                return Failure(new GraphDiagnostic(
                    "atproto.http.status",
                    $"The AT record endpoint returned {(int)response.StatusCode}.",
                    DiagnosticSeverity.Error,
                    uri.ToString()));
            }

            if (response.Content.Headers.ContentLength > options.MaxResponseBytes)
            {
                return Failure(new GraphDiagnostic(
                    "atproto.response.limit",
                    "The AT record response exceeds the configured byte limit.",
                    DiagnosticSeverity.Error,
                    uri.ToString()));
            }

            string json;
            try
            {
                json = await ReadBoundedAsync(
                    response,
                    options.MaxResponseBytes,
                    timeoutCancellation.Token);
            }
            catch (InvalidDataException exception)
            {
                return Failure(new GraphDiagnostic(
                    "atproto.response.limit",
                    exception.Message,
                    DiagnosticSeverity.Error,
                    uri.ToString()));
            }

            try
            {
                var envelope = AtRecordEnvelopeDecoder.Decode(json);
                if (envelope.Uri.Did != uri.Did ||
                    envelope.Uri.Collection != uri.Collection ||
                    envelope.Uri.Rkey != uri.Rkey)
                {
                    return Failure(new GraphDiagnostic(
                        "atproto.response.identity",
                        "The AT record response URI does not match the requested record.",
                        DiagnosticSeverity.Error,
                        uri.ToString()));
                }

                return new OperationResult<AtRecordEnvelope>(envelope);
            }
            catch (JsonException exception)
            {
                return Failure(new GraphDiagnostic(
                    "atproto.response.invalid",
                    $"The AT record response was invalid JSON: {exception.Message}",
                    DiagnosticSeverity.Error,
                    uri.ToString()));
            }
            catch (FormatException exception)
            {
                return Failure(new GraphDiagnostic(
                    "atproto.response.invalid",
                    $"The AT record response contained an invalid AT URI: {exception.Message}",
                    DiagnosticSeverity.Error,
                    uri.ToString()));
            }
            catch (ArgumentException exception)
            {
                return Failure(new GraphDiagnostic(
                    "atproto.response.invalid",
                    $"The AT record response failed validation: {exception.Message}",
                    DiagnosticSeverity.Error,
                    uri.ToString()));
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(new GraphDiagnostic(
                "atproto.timeout",
                "The AT record request exceeded its timeout.",
                DiagnosticSeverity.Error,
                uri.ToString()));
        }
        catch (HttpRequestException exception)
        {
            return Failure(new GraphDiagnostic(
                "atproto.http.failure",
                $"The AT record request failed: {exception.Message}",
                DiagnosticSeverity.Error,
                uri.ToString()));
        }
    }

    /// <summary>Disposes the owned production transport or explicit test transport.</summary>
    public void Dispose() => transport.Dispose();

    private Uri BuildRequestUri(AtUri uri)
    {
        var query = string.Join(
            "&",
            $"repo={Uri.EscapeDataString(uri.Did)}",
            $"collection={Uri.EscapeDataString(uri.Collection)}",
            $"rkey={Uri.EscapeDataString(uri.Rkey)}");
        var builder = new UriBuilder(serviceEndpoint)
        {
            Query = query
        };
        return builder.Uri;
    }

    private static async Task<string> ReadBoundedAsync(
        HttpResponseMessage response,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        var total = 0;
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            total += read;
            if (total > maximumBytes)
            {
                throw new InvalidDataException("The AT record response exceeds the configured byte limit.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void ValidateEndpointShape(Uri endpoint)
    {
        if (!endpoint.IsAbsoluteUri ||
            !endpoint.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(endpoint.UserInfo) ||
            !string.IsNullOrEmpty(endpoint.Query) ||
            !string.IsNullOrEmpty(endpoint.Fragment) ||
            string.IsNullOrWhiteSpace(endpoint.Host))
        {
            throw new ArgumentException(
                "The AT record service endpoint must be an absolute HTTPS URI without credentials, query, fragment, or an empty host.",
                nameof(endpoint));
        }
    }

    private static OperationResult<AtRecordEnvelope> Failure(GraphDiagnostic diagnostic) =>
        new(null, [diagnostic]);
}

/// <summary>Production transport with direct, validated, IP-pinned socket connections.</summary>
internal sealed class SecureAtRecordTransport : IAtRecordTransport
{
    private readonly HttpMessageInvoker invoker;

    private SecureAtRecordTransport(HttpMessageInvoker invoker) =>
        this.invoker = invoker;

    public static SecureAtRecordTransport Create(AtEndpointPolicy policy)
    {
        return new SecureAtRecordTransport(new HttpMessageInvoker(CreateHandler(policy)));
    }

    internal static SocketsHttpHandler CreateHandler(AtEndpointPolicy policy) =>
        new()
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            Credentials = null,
            PreAuthenticate = false,
            ConnectCallback = (context, cancellationToken) =>
                ConnectValidatedAsync(policy, context, cancellationToken)
        };

    public ValueTask<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        new(invoker.SendAsync(request, cancellationToken));

    public void Dispose() => invoker.Dispose();

    private static async ValueTask<Stream> ConnectValidatedAsync(
        AtEndpointPolicy policy,
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var endpoint = context.DnsEndPoint;
        var addresses = await policy.ResolveValidatedAddressesAsync(
            endpoint.Host,
            endpoint.Port,
            cancellationToken);
        if (!addresses.IsSuccess || addresses.Value.IsDefaultOrEmpty)
        {
            throw new HttpRequestException(
                addresses.Diagnostics.FirstOrDefault()?.Message ??
                "The destination did not pass endpoint validation.");
        }

        Exception? lastException = null;
        foreach (var address in addresses.Value)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            try
            {
                await socket.ConnectAsync(
                    new IPEndPoint(address, endpoint.Port),
                    cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (OperationCanceledException)
            {
                socket.Dispose();
                throw;
            }
            catch (SocketException exception)
            {
                socket.Dispose();
                lastException = exception;
            }
        }

        throw new HttpRequestException(
            "No validated endpoint address accepted the connection.",
            lastException);
    }
}
