# Privacy model

## Principles

- Local achievement records remain local by default.
- Online account queries require an enabled online source.
- Per-game settings override global defaults.
- Disabling tracking stops scans, monitoring, exports, and display updates for that game.
- Source diagnostics must explain what was contacted and what was read.

## Tracking modes

- **Inherit**: use the global mode.
- **Automatic**: select the best capable source according to configured precedence.
- **Native only**: use enabled platform-native sources only.
- **Local only**: use local sources and avoid online achievement queries by this plugin.
- **Hybrid**: combine enabled native and local sources.
- **Disabled**: perform no achievement work for the game.

## Important limitation

Local-only mode controls this plugin. It does not disable networking inside a game or platform client. The plugin must not claim that Steam, GOG, Epic, or another platform cannot receive activity unless the launch environment independently guarantees that condition.

## Sensitive data

The repository must not contain real account IDs, authentication data, usernames from source files, machine-specific paths, or private achievement databases. Test fixtures must be synthetic or sanitized.

Optional API credentials are stored only in the Playnite plugin settings area. Steam Web API keys are protected with Windows Data Protection API using the current-user scope before Playnite serializes the settings. The plain key is excluded from serialization and must never be written to logs, diagnostics, exports, fixtures, or crash messages.

Steam schema lookup is disabled by default and runs only from the explicitly selected
metadata-preparation action. The client uses HTTPS with bounded timeout and response
size. Only validated, non-secret definition metadata is cached under this extension's
own data directory; the credential and request URL are not cached or reported.
