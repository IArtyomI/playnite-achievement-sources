# Provider contract

## Internal contract

An achievement source is responsible for reading one family of achievement data and returning normalized records.

Required behavior:

- `Key` is stable across releases.
- `IsCapable` is fast and side-effect free.
- `ReadAsync` is read-only with respect to the source.
- Unsupported layouts return diagnostics rather than guessed data.
- Unlock timestamps remain absent when the source does not provide them.
- Partial snapshots never cause existing achievements to be deleted.

## Normalized identity

A source record is identified by:

```text
source family + source game ID + achievement ID
```

Cross-source reconciliation may map several source identities to one canonical achievement, but raw evidence must remain available for diagnostics.

## External export

The first external format will be a versioned JSON document written to plugin-owned storage. A future consumer should be able to read the document without loading this plugin's assembly.

Proposed envelope:

```json
{
  "schemaVersion": 1,
  "generatedAtUtc": "2026-07-28T21:00:00Z",
  "playniteGameId": "00000000-0000-0000-0000-000000000000",
  "trackingMode": "LocalOnly",
  "sources": [],
  "achievements": []
}
```

The schema will be finalized only after the first real source adapter produces fixtures.
