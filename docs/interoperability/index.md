# Interoperability

The wire format is intentionally simple enough for clients that do not use
.NET. Interoperable clients should preserve the following without interpreting
more than they support:

- the exact `$type` string;
- bundle member order and each non-negative `position`;
- the reference `uri`;
- recognized `format`, `cid`, `mode`, `title`, and `label` fields;
- unknown reference envelopes and unknown fields when round-tripping.

The [draft wire format](../protocol/wire-format.md) describes the contract.
The examples are small synthetic JSON files that can be tested offline.

Choose an implementation style:

- [.NET sample](dotnet.md) for the immutable Core model and deterministic JSON.
- [TypeScript sample](typescript.md) for structural parsing and preservation.

No official TypeScript package is published in this slice. The TypeScript
example is deliberately dependency-free and should be paired with schema or
application validation.
