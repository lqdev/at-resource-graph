# Security and operational constraints

Treat every URI, feed field, title, description, thumbnail, and enclosure as
untrusted input. The draft favors explicit limits and diagnostics over
best-effort network behavior.

## Network policy

Resolvers must:

- default to HTTPS and allow only documented ports;
- honor cancellation and timeouts;
- cap redirects, response bytes, decompression, and concurrency;
- validate DNS results before connecting and after every redirect;
- reject loopback, private, link-local, multicast, reserved, and metadata-service
  destinations by default;
- avoid forwarding cookies, authorization headers, and app credentials.

The public AT resolver uses its own no-proxy, no-redirect socket transport. Its
connection callback validates the exact IP set while keeping the original host
for TLS/SNI. Redirect responses are rejected and their `Location` is not
contacted. Arbitrary handler chains are not accepted in production.

## Parsing and rendering

HTML discovery scans only metadata in the document head, stops at `</head>` or
its byte cap, and ignores body content, scripts, JSON-LD, Open Graph, and
anchors. Feed XML disables DTD processing and external resolution. Renderers
must escape untrusted text and sanitize feed HTML before treating it as markup.

## Graph limits

Nested expansion must bound depth, member count, response bytes, and expanded
items. Cycle detection uses stable identities. A partial result carries an
explicit diagnostic when policy allows it. The implementation defaults and
diagnostic vocabulary are described in the generated APIs and the local
[security specification](../../specs/SECURITY.md).

## Reader boundary

The offline Reader exposes only a local sample bundle, a parsed local sample
feed, and bounded JSON validation. It does not accept arbitrary source URLs or
fetch feeds server-side. Responses set content type, framing, referrer,
permissions, and content-security headers.

This page is a practical summary. The canonical security rules remain in the
local [resource resolution security document](../../specs/SECURITY.md), and
private vulnerability reports use the root [security policy](../../SECURITY.md).
