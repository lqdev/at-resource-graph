# Security policy

This project is an early bootstrap and does not yet operate a hosted service.
Please do not disclose a suspected vulnerability in a public issue when it may
contain credentials, private URLs, or an exploitable request.

Report security issues privately to the repository maintainers through the
security contact configured when the future GitHub repository is created.
Include a concise description, affected path or component, reproduction steps,
and a suggested mitigation when known.

The project treats SSRF, unsafe redirects, decompression or response-size
exhaustion, credential forwarding, XSS in rendered metadata, and unbounded
nested-bundle expansion as security concerns. See
[specs/SECURITY.md](specs/SECURITY.md) for the protocol and implementation
boundaries.
