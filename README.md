# Achievement Sources for Playnite

Achievement Sources is an independent Playnite extension for discovering, reading, and normalizing achievement data from local and online-compatible sources.

The project is designed to complement achievement display extensions rather than replace them. It will provide per-game source control, privacy-conscious local tracking, diagnostics, and a versioned integration format that other Playnite extensions can consume.

## Project status

Early development. The repository currently contains the architectural foundation and a loadable Playnite plugin shell. No installable release is available yet.

## Goals

- Select achievement behavior per game: inherit, automatic, native only, local only, hybrid, or disabled.
- Detect supported achievement sources without requiring one specific runtime or folder layout.
- Normalize achievement definitions, unlock state, progress, timestamps, and source evidence.
- Keep local achievement data on the user's computer by default.
- Provide clear diagnostics when a source is missing, unsupported, or ambiguous.
- Integrate with Playnite Achievements through a narrow, versioned bridge when that integration becomes available.
- Support additional local and platform sources incrementally, beginning with common Steam-compatible formats.

## Non-goals

- Bundling or installing platform emulation binaries.
- Modifying game executables or replacing platform DLLs.
- Writing to GOG Galaxy, Steam, or other platform databases.
- Claiming that a platform cannot receive activity when the game itself remains connected to that platform.
- Replacing the standard Playnite Achievements extension.

## Development

Requirements:

- Windows 10 or 11
- Visual Studio 2022 with .NET Framework 4.6.2 targeting support
- Playnite

Build the solution:

```powershell
msbuild AchievementSources.sln /restore /p:Configuration=Debug
```

Add the build output directory to Playnite under **Settings > For developers > External extensions**.

## Documentation

- [Architecture](docs/architecture.md)
- [Provider contract](docs/provider-contract.md)
- [Privacy model](docs/privacy-model.md)
- [Supported formats](docs/supported-formats.md)
- [Separate-plugin decision](docs/adr/0001-separate-plugin.md)
