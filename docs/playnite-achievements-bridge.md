# Playnite Achievements bridge proposal

## Current limitation

Playnite Achievements currently discovers `IDataProvider` implementations only from its own assembly. A separate Playnite extension therefore cannot register a provider without either replacing/forking Playnite Achievements or adding a small upstream extension point.

Achievement Sources does not replace the Playnite Achievements extension identity and does not write directly to its internal database.

## File bridge

Version 0.5 prototypes a producer-neutral, read-only file boundary:

```text
<Playnite ExtensionsDataPath>\<producer plugin id>\
  bridge\v1\index.json
  snapshots\v1\<playnite game id>.json
```

The index format is `playnite-achievement-sources.index` schema version `1`. Each entry contains:

- the Playnite game ID;
- the snapshot path relative to the producer's extension-data root;
- source identity;
- snapshot generation time;
- achievement count;
- `StateKnown`;
- `IsCompleteSnapshot`.

Snapshot paths are relative by design. Consumers must reject rooted paths, traversal outside the producer root, non-JSON paths, missing files, unsupported schemas, and game-ID mismatches.

## Consumer semantics

A generic external-snapshot provider in Playnite Achievements should apply these rules:

- `StateKnown = false`: definitions may be displayed, but no locked/unlocked conclusion is authoritative. Existing state must not be cleared.
- `StateKnown = true`, `IsCompleteSnapshot = false`: merge only explicitly represented unlock/progress information. Missing records are not evidence of a locked state.
- `StateKnown = true`, `IsCompleteSnapshot = true`: the snapshot can be treated as an authoritative point-in-time state for that producer and game.
- unknown fields are ignored for forward compatibility;
- unsupported major schema versions are rejected;
- source/evidence paths remain local and must not be uploaded or written to normal diagnostics.

## Minimal upstream change

The preferred Playnite Achievements change is one built-in `ExternalSnapshotDataProvider` that scans immediate extension-data roots for `bridge\v1\index.json` and consumes compliant producers. This avoids binary references between plugins and supports multiple independent producers.

An alternative is a public runtime registration method on `ProviderRegistry`, but that would require a shared binary contract and stricter assembly-version coordination. The file bridge is more stable for independently released Playnite extensions.

## Prototype boundary

Achievement Sources now publishes and validates the producer side of this contract. It does not yet modify Playnite Achievements, trigger its refresh pipeline, write its cache, or display its notifications.
