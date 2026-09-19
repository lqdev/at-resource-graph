# Changelog

## Unreleased

### Added

- SSRF-safe AT record resolution with no-redirect handler requirements,
  HTTPS/port/DNS endpoint validation, and explicit redirect rejection.
- Strict public-read AT URI, DID, handle, collection, and CID validation.
- Bounded `com.atproto.repo.getRecord` decoding through an injected HTTPS
  `HttpClient`, with explicit response diagnostics and no credential forwarding.
- Standard.site verification/document metadata adaptation from the existing
  HTML-head discovery result.
- Public Bluesky post/profile record projections while keeping RSS and Atom on
  the Syndication path.
- An offline ASP.NET Core reference reader with sample bundle/feed endpoints,
  bounded bundle validation, security headers, and no arbitrary URL fetching.
- AppView and Aspire read/topology contracts without service deployment.
