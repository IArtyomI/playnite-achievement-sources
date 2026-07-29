# Achievement Sources for Playnite

Achievement Sources is an independent Playnite extension for discovering, reading, and normalizing achievement data from local and online-compatible sources.

The project is designed to complement achievement display extensions rather than replace them. It provides per-game source control, privacy-conscious local tracking, diagnostics, and a versioned integration format that other Playnite extensions can consume.

## Project status

GBE-compatible v1 development candidate. The producer publishes a producer-neutral
snapshot catalog under Playnite's extension-data directory and monitors only resolved
definition/state locations. The companion Playnite Achievements draft adds automatic
single-game import. Release packaging is not enabled.

## Goals

- Select achievement behavior per game: inherit, automatic, native only, local only, hybrid, or disabled.
- Detect supported achievement sources without requiring one specific runtime or folder layout.
- Normalize achievement definitions, unlock state, progress, timestamps, and source evidence.
- Keep local achievement data on the user's computer by default.
- Provide clear diagnostics when a source is missing, unsupported, or ambiguous.
- Integrate with Playnite Achievements through a narrow, versioned file bridge.
- Support additional local and platform sources incrementally, beginning with common Steam-compatible formats.

## Non-goals

- Bundling or installing platform emulation binaries.
- Modifying game executables or replacing platform DLLs.
- Writing to GOG Galaxy, Steam, or other platform databases.
- Writing or fabricating achievement unlocks.
- Claiming that a platform cannot receive activity when the game itself remains connected to that platform.
- Replacing the standard Playnite Achievements extension.

## Development

Requirements:

- Windows 10 or 11
- Visual Studio 2022 with .NET Framework 4.6.2 targeting support
- Playnite

Run the local validation workflow:

```powershell
.\scripts\validate-local.ps1
```

The script restores and rebuilds the solution, verifies the Playnite extension output, and runs test projects when present. Hosted GitHub Actions are intentionally not used.

After the build succeeds, add the printed output directory in Playnite under **Settings > For developers > External extensions** and complete the manual load checklist.

Right-click a single game and use:

- **Achievement Sources > Inspect Steam AppID sources** for AppID evidence;
- **Achievement Sources > Inspect local achievement data** for read-only GBE/Goldberg-compatible definition and state diagnostics;
- **Achievement Sources > Write local snapshot** to publish a schema-v1 snapshot and update the local bridge catalog.
- **Achievement Sources > Prepare GBE-compatible achievement metadata...** to dry-run and explicitly confirm importing user-selected definition JSON into an existing `steam_settings` directory. The global write permission is disabled by default.
- **Achievement Sources > Select explicit runtime-state file...** when automatic save-root resolution cannot identify the emulator-owned state.

The preparation action may write only installation-side definition metadata. It never
creates or modifies emulator runtime state. `stats.json` is not generated in v1 because
achievement definition import does not require it and the plugin does not guess stat
semantics.

## Documentation

- [Architecture](docs/architecture.md)
- [Provider contract](docs/provider-contract.md)
- [Privacy model](docs/privacy-model.md)
- [Supported formats](docs/supported-formats.md)
- [Playnite Achievements bridge](docs/playnite-achievements-bridge.md)
- [Local validation](docs/local-validation.md)
- [Separate-plugin decision](docs/adr/0001-separate-plugin.md)
