# Resource resolution security

This document is a normative security skeleton for future adapters.

## Network boundaries

Resolvers MUST default to HTTPS, honor cancellation and timeouts, cap
redirects, enforce response and decompression limits, and bound concurrency.
They MUST NOT forward user cookies, authorization headers, or app credentials
to external sources.

Resolvers MUST apply a safe DNS and IP policy before connecting and after
redirects. Private, loopback, link-local, and metadata-service destinations
are denied by default. A deployment MAY make a narrowly documented exception
for a controlled development topology.

## Discovery and rendering

HTML discovery is optional and limited to metadata in the document head. Page
body scraping is out of scope. Discovered strings MUST be treated as untrusted
data and escaped by renderers. Feed HTML, titles, descriptions, thumbnails,
and enclosure URLs MUST NOT become executable markup without sanitization.

The executable discovery parser stops at `</head>` or its configured UTF-8
byte cap. It parses only metadata `link` and `meta` elements and removes
script/style regions before matching. It rejects non-HTTPS external links and
loopback, private, link-local, and known metadata-service destinations. AT
URI metadata is accepted as a non-network reference and is not fetched.

The public AT record resolver constructs its own `SocketsHttpHandler` with
proxy use, automatic redirects, cookies, credentials, and pre-authentication
disabled. Its `ConnectCallback` resolves and validates the destination for the
actual socket connection, then connects only to the validated IP set while the
request retains its original hostname for TLS/SNI. Every original and final
request endpoint must use HTTPS, an allowlisted port, and a host that does not
resolve to loopback, private, link-local, multicast, metadata-service,
IPv4-mapped private, or reserved space. Any 3xx response is rejected and its
`Location` target is never contacted.

Arbitrary `HttpMessageHandler` chains are rejected because a delegating or
custom handler can rewrite requests or route through an uninspected proxy. The
explicit `CreateForTesting` transport boundary is the only injectable path,
and production construction cannot use it.

The reference Reader exposes only a local sample bundle, a parsed local sample
feed, and bounded JSON validation. It does not accept arbitrary source URLs or
perform server-side feed fetching. Responses include content-type, framing,
referrer, permissions, and content-security headers.

## Graph limits

Nested bundle expansion MUST enforce depth, members, bytes, and expanded-item
limits. Cycle detection MUST use stable reference identity where available.
Partial results MUST carry explicit diagnostics when policy allows them.

## Reporting

Security reports belong in the private channel described by the root
[SECURITY.md](../SECURITY.md).
