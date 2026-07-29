# Playnite Achievements bridge

Achievement Sources does not replace the Playnite Achievements extension identity and
does not write directly to its internal database. The companion draft implements a
normal built-in `ExternalSnapshotDataProvider`.

## File bridge

The producer-neutral, read-only file boundary is:

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

The External Snapshot provider applies these rules:

- `StateKnown = false`: definitions may be displayed, but no locked/unlocked conclusion is authoritative. Existing state must not be cleared.
- `StateKnown = true`, `IsCompleteSnapshot = false`: discoverable but not cache-authoritative; no cache write occurs.
- `StateKnown = true`, `IsCompleteSnapshot = true`: the snapshot can be treated as an authoritative point-in-time state for that producer and game.
- unknown fields are ignored for forward compatibility;
- unsupported major schema versions are rejected;
- source/evidence paths remain local and must not be uploaded or written to normal diagnostics.

## Automatic update flow

The producer debounces resolved `achievements.json` changes, waits before re-reading,
rejects malformed/partial input, and atomically updates its snapshot and catalog only
when normalized content changes. The consumer scans catalogs on a bounded interval,
debounces per-game work, and requests only an `ExternalSnapshot` refresh for the changed
Playnite game through the existing refresh coordinator and cache manager.

The first authoritative import is a baseline and does not emit unlock notifications.
Later locked-to-unlocked differences raise the existing Playnite Achievements
`AchievementUnlocked` event; its existing notification policy determines whether a
toast appears. Unchanged refreshes emit nothing. The producer never calls notification
services.

When multiple producers expose a game, the consumer deterministically selects the
newest valid authoritative snapshot. Producer disappearance or malformed replacement
does not delete cached state. No component writes directly to the achievement database.

Real-world acceptance must use an isolated Playnite profile. The ordinary profile must
not contain both the development extension and another Playnite Achievements fork.
