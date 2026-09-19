# AT record integration

AT records are referenced by canonical `at://` text and resolved only at a
public-read boundary. A bundle does not embed an AT record value.

## URI shape

```text
at://<DID>/<collection NSID>/<record key>?cid=<optional CID>
```

`AtUri` validates the DID, collection, and record key while preserving the
colons in a DID authority. A CID identifies an observed version; the reference
identity remains the canonical AT URI.

## Public record envelope

The public `com.atproto.repo.getRecord`-style response must contain:

```json
{
  "uri": "at://did:plc:example123/app.example.post/3jz7example",
  "cid": "bafyexample",
  "value": {
    "$type": "app.example.post",
    "text": "Synthetic public record"
  }
}
```

The decoder validates `uri`, `cid`, and an object-valued `value` before
exposing an `AtRecordEnvelope`. A missing or malformed field is an error, not an
empty record.

## Adapter boundary

`ResourceGraph.AtProto` accepts an injected HTTPS service endpoint and applies
bounded response, cancellation, and endpoint policy checks. It does not send
authorization or cookie headers. `ResourceGraph.Bluesky` decodes only the
small public post/profile shapes implemented by this slice; RSS/Atom remains
ordinary syndication input.

OAuth, writes, firehose ingestion, PDS deployment, and AppView projection are
out of scope. The generated [AT Protocol API](../reference/api.md) is the
implementation reference. Normative URI and envelope rules are in the local
[reference specification](../../specs/REFERENCES.md).
