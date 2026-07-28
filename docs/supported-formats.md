# Supported formats

## GBE Fork / Goldberg-compatible JSON

Status: **initial read-only support**

The plugin can now inspect:

- achievement definitions stored as a JSON array in an installation-side `achievements.json`, with `steam_settings` paths preferred;
- AppID-scoped user state stored as a JSON object in `achievements.json` beneath `GSE Saves` or the legacy `Goldberg SteamEmu Saves` root;
- `earned`, `earned_time`, `progress`, and `max_progress` state;
- localized `displayName` and `description` values;
- `icon`, `icon_gray`, and legacy `icongray` references.

The implementation is read-only. It does not create definitions, modify unlock state, alter binaries, or write to platform databases.

A missing state file is represented as an incomplete snapshot rather than treated as definitive proof that every achievement is locked.

## Planned adapters

| Source | Status | Notes |
|---|---|---|
| GBE Fork / Goldberg-compatible state | Initial read-only support | Definitions and AppID-scoped state inspection with sanitized tests. |
| Legacy Goldberg layouts | Partial | Legacy save-root and `icongray` support; additional representative fixtures still needed. |
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
