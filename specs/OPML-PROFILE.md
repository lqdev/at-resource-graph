# OPML profiles

AT Resource Graph defines two OPML profiles. Both use ordinary OPML 2.0
outlines and synthetic examples; neither changes the OPML format.

## Feed profile

The **feed profile** is a strict interoperability projection. RSS and Atom
references become feed outlines with `xmlUrl` and `type` values. Resources that
are not feeds MAY be omitted, and the exporter MUST report the omission.

## Graph profile

The **graph profile** preserves more references by using `type="link"` and
`url` for website, AT, and nested-bundle references. It MAY add a namespaced
metadata field for a stable reference URI, but consumers that do not know that
field must still receive a useful link.

The executable graph profile uses the `rg` namespace
`https://lqdev.dev/at-resource-graph/opml` with `rg:position`,
`rg:kind`, `rg:uri`, `rg:label`, `rg:format`, `rg:cid`, and `rg:mode`
attributes where applicable. These fields preserve order, reference kind,
canonical AT text, feed format, CIDs, nested-bundle mode, and labels.

Both profiles lose authorship, CIDs, membership relationships, nested
provenance, and live/frozen semantics unless a separate bundle export
accompanies the OPML file. Exporters MUST state this loss.
