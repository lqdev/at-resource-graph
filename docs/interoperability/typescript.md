# TypeScript interoperability

The draft JSON can be consumed structurally without a .NET dependency. Keep
the discriminator and unknown fields so a newer client can round-trip a
reference it does not understand.

```ts
type KnownReference =
  | {
      $type: "me.lqdev.resourcegraph.temp.defs#syndicationRef";
      uri: string;
      format: "rss" | "atom";
      title?: string;
    }
  | {
      $type: "me.lqdev.resourcegraph.temp.defs#atRecordRef";
      uri: string;
      cid?: string;
      title?: string;
    }
  | {
      $type: "me.lqdev.resourcegraph.temp.defs#bundleRef";
      uri: string;
      mode?: "live" | "frozen";
      title?: string;
    };

type Membership = {
  position: number;
  label?: string;
  resource: KnownReference | Record<string, unknown>;
};

type Bundle = {
  $type: "me.lqdev.resourcegraph.temp.bundle";
  name: string;
  description?: string;
  uri?: string;
  members: Membership[];
};

export function orderedMembers(bundle: Bundle): Membership[] {
  return [...bundle.members].sort((left, right) => left.position - right.position);
}

const bundle = JSON.parse(
  await (await fetch("./examples/valid/nested-bundle.json")).text(),
) as Bundle;

console.log(orderedMembers(bundle).map(member => member.resource.$type));
```

This sample is intentionally not a complete validator. Check the `$type`,
absolute URI scheme, unique non-negative positions, and reference-specific
fields before using data. Preserve unknown objects instead of dropping them.
For exact field rules, use the local [Lexicon files](../protocol/lexicons.md)
and [wire-format guide](../protocol/wire-format.md).

OPML is a separate projection. A TypeScript reader should not assume that an
OPML round-trip preserves CIDs, nested bundle mode, or provenance; see
[OPML loss semantics](../integrations/opml.md).
