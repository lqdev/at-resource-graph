# Examples

The files under `valid/` are small, synthetic examples of the draft bundle
shape. `nested-bundle.json` demonstrates a direct feed, a nested bundle, and an
AT record in one ordered membership list.

The file under `invalid/` is intentionally **schema-invalid but syntactically
valid JSON**. It omits the required `position` field so future conformance
tests can exercise semantic validation without making the JSON parser test
special-case malformed source text.
