# Local snapshot format

Achievement Sources writes snapshots only to its Playnite extension-data directory. It never writes snapshots into game, Steam, GOG, or runtime folders.

## Version 1

The format identifier is `playnite-achievement-sources.snapshot` and the schema version is `1`.

Important fields:

- `PlayniteGameId`: stable Playnite database identifier used as the snapshot filename.
- `OverrideMode`: explicit per-game mode, or `Inherit`.
- `EffectiveTrackingMode`: resolved mode after applying the global default.
- `SourceKey` and `SourceGameId`: adapter identity and source-specific game identifier.
- `StateKnown`: whether a supported user-state file was actually read.
- `IsCompleteSnapshot`: whether the source can honestly represent the current state of every definition.
- `Achievements`: normalized achievement records.
- `Diagnostics`: non-secret source and completeness explanations.

`StateKnown: false` and `IsCompleteSnapshot: false` must never be interpreted as every achievement being locked. It means the definition catalog was available but unlock state was unknown.

Snapshots are replaced atomically through a temporary file and are stored under:

```text
<Playnite extension data>\0391911C-BF98-4A2D-8200-8641AF0973E9\snapshots\v1\<playnite-game-id>.json
```

This boundary is intended for future external-provider integration. It is not yet consumed by Playnite Achievements.
