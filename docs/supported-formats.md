# Supported formats

## GBE Fork / Goldberg-compatible JSON

Status: **GBE-compatible v1 development candidate**

The plugin can now inspect:

- achievement definitions stored as a JSON array in an installation-side `achievements.json`, with `steam_settings` paths preferred;
- AppID-scoped user state stored as a JSON object in `achievements.json` beneath `GSE Saves` or the legacy `Goldberg SteamEmu Saves` root;
- `earned`, `earned_time`, `progress`, and `max_progress` state;
- localized `displayName` and `description` values;
- `icon`, `icon_gray`, and legacy `icongray` references.

Runtime state access is always read-only. Definition metadata remains read-only by
default. If the global preparation permission is enabled, a single-game command can
import an explicitly selected, validated definition array into an existing recognized
`steam_settings` directory after showing every proposed path and receiving confirmation.
Replacement creates a timestamped backup and uses an atomic write followed by read-back
validation. When online lookup is explicitly enabled and a protected Steam Web API key
is configured, the same command can retrieve the game's public achievement schema from
Steam's official `ISteamUserStats/GetSchemaForGame/v2` endpoint. Validated non-secret
responses are cached only under this extension's data directory. Local JSON import
remains the offline fallback. It does not generate `stats.json`.

A missing state file is represented as an incomplete snapshot rather than treated as definitive proof that every achievement is locked.

## Definitions are not runtime state

Installation-side `steam_settings\achievements.json` defines achievement names,
descriptions, hidden flags, localization, and icons. Emulator-owned
`<save root>\<AppID>\achievements.json` records the user's earned state, timestamps,
and progress. A definition file never proves that runtime state is known. The plugin
never creates or changes the latter file.

## Save layouts

The resolver supports the Windows defaults `%APPDATA%\GSE Saves\<AppID>` and
`%APPDATA%\Goldberg SteamEmu Saves\<AppID>`. For GBE Fork configuration it reads
`[user::saves]` values `local_save_path` and `saves_folder_name` from a recognized
`steam_settings\configs.user.ini`, including portable relative paths. A per-game
explicit state file can be selected when automatic resolution fails; a numeric parent
directory that conflicts with the detected AppID is rejected.

A nonstandard definition file outside recognized `steam_settings` layouts is never
accepted by broad discovery. It can be selected explicitly per game after schema
validation, and remains read-only.

Regular and experimental GBE/Goldberg variants are treated alike when they use these
validated files. Overlay availability is irrelevant and no emulator binary is installed
or replaced.

## Tracking modes

- `Automatic`: safely choose the best supported source currently available.
- `NativeOnly`: do not inspect, monitor, or prepare local GBE-compatible data.
- `LocalOnly`: use supported local data only.
- `Hybrid`: permit local state and separately permitted metadata enrichment.
- `Disabled`: do nothing.
- `Inherit`: use the global default for that game.

Metadata write permission is independent. `LocalOnly` never implies permission to write.

Online metadata controls are disabled by default. Enabling both online metadata lookup
and Steam schema use permits definition retrieval only during the explicitly invoked
preparation command. Requests use HTTPS, bounded time and response size, and never log,
export, cache, or display the API key. The key is protected with Windows current-user
data protection in Playnite settings. Online definitions never supply or infer runtime
unlock state.

## Planned adapters

| Source | Status | Notes |
|---|---|---|
| GBE Fork / Goldberg-compatible state | Development candidate | Definition/state reading, configured save roots, monitoring, and sanitized tests. |
| Legacy Goldberg layouts | Partial | Legacy save-root and `icongray` support; representative real-game acceptance remains. |
| Generic Steam-compatible JSON | Planned | Strict schema detection; no broad guessing. |
| GOG online | Research | Existing platform behavior and identifiers will be evaluated. |
| GOG Galaxy local state | Research | Read-only investigation; no Galaxy database writes. |

## Support criteria

A format is considered fully supported only when:

- detection is deterministic;
- fixtures cover locked and unlocked states;
- malformed or partial writes fail safely;
- timestamps and progress are represented honestly;
- automated tests verify normalization;
- diagnostics identify the selected adapter and evidence path;
- representative real-world layouts have been manually validated.

The project will not bundle source runtimes, platform DLLs, account credentials, or game files.
