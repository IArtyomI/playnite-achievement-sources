# Local validation

This repository intentionally does not use GitHub-hosted Actions. Builds and tests are run on a Windows development machine so they do not consume repository Actions usage and can be followed by a real Playnite load test.

## Requirements

- Windows 10 or 11
- Visual Studio 2022 or Visual Studio Build Tools
- .NET Framework 4.6.2 targeting pack
- Playnite

Docker is not required for the plugin build. The extension targets the Windows-only Playnite desktop SDK and .NET Framework, so the primary validation environment is the Windows host rather than a Linux Docker container.

## Automated local checks

From the repository root, run:

```powershell
.\scripts\validate-local.ps1
```

The script:

1. resolves MSBuild from the command line or Visual Studio installation;
2. cleans previous `bin` and `obj` directories;
3. restores dependencies;
4. rebuilds the solution in Release mode;
5. verifies that the plugin assembly and `extension.yaml` were produced;
6. runs test projects when they are present.

Use a Debug build when needed:

```powershell
.\scripts\validate-local.ps1 -Configuration Debug
```

## Playnite load test

After the script succeeds:

1. Open Playnite.
2. Open **Settings > For developers > External extensions**.
3. Add the output directory printed by the script.
4. Restart Playnite.
5. Confirm that **Achievement Sources** appears in the Add-ons list.
6. Open the extension's development-status menu item.
7. Confirm that Playnite shows no extension-load or unhandled-exception notification.
8. Close Playnite and inspect `playnite.log` for errors mentioning `PlayniteAchievementSources`.

## Pull-request evidence

Before a pull request is marked ready, record:

- the commit SHA tested;
- the validation command used;
- whether the build and tests passed;
- whether the plugin loaded in Playnite;
- any relevant sanitized log excerpt.

Do not commit machine-specific paths, account identifiers, API credentials, or unsanitized game files.
