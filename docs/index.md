# AT Resource Graph

AT Resource Graph is a draft, protocol-aware graph for ordered collections of
RSS/Atom feeds, AT Protocol records, websites, and nested bundles. This site
explains the executable offline slice and the draft wire format without
replacing the canonical specifications.

> [!IMPORTANT]
> The current Lexicons use the incubation namespace
> `me.lqdev.resourcegraph.temp`. The format is a draft, not a community
> standard. Treat the normative documents as versioned protocol material.

## Choose a task

| If you want to… | Start here |
| --- | --- |
| Run the offline CLI and validate a bundle | [Quickstart](getting-started/quickstart.md) |
| Understand bundles, references, and provenance | [Architecture and resource model](concepts/architecture.md) |
| Read or produce bundle JSON | [Draft wire format](protocol/wire-format.md) |
| Use the draft AT Lexicons | [Lexicons](protocol/lexicons.md) |
| Normalize RSS or Atom | [Syndication integration](integrations/syndication.md) |
| Resolve a public AT record | [AT record integration](integrations/at-records.md) |
| Exchange collections with feed readers | [OPML profiles](integrations/opml.md) |
| Configure network and expansion limits | [Security and operations](operations/security.md) |
| Interoperate from .NET or TypeScript | [Interoperability samples](interoperability/index.md) |
| Find CLI commands, APIs, and conformance material | [Reference](reference/index.md) |

## What is implemented

The current slice provides immutable Core models, deterministic bundle JSON,
RSS/Atom parsing, bounded HTML head discovery, public AT record envelopes,
nested-bundle expansion diagnostics, OPML import/export, and an offline Reader
surface. It does not provide OAuth, writes, firehose ingestion, AppView
projection, or arbitrary URL fetching.

The documentation separates three layers:

1. **Conceptual guidance** explains how to make and consume a graph.
2. **Canonical specifications** retain the normative draft prose under
   [Specifications](reference/specifications.md).
3. **Generated references** describe the public .NET APIs and offline CLI.

## Project status

This is incubation documentation. Examples use synthetic hosts and identities.
The executable tests and examples do not call external feeds or AT services.
For contribution boundaries, see the local [governance policy](../GOVERNANCE.md)
and [security policy](../SECURITY.md).

Source links are provided for editing and history only. The information needed
to use the draft is intentionally available in this site.
