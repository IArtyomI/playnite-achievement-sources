# Supported formats

No achievement format is supported in the foundation build.

## Planned first adapters

| Source | Status | Notes |
|---|---|---|
| GBE Fork / Goldberg-compatible state | Planned | First implementation target using sanitized fixtures. |
| Legacy Goldberg layouts | Planned | Added only after representative fixtures are available. |
| Generic Steam-compatible JSON | Planned | Strict schema detection; no broad guessing. |
| GOG online | Research | Existing platform behavior and identifiers will be evaluated. |
| GOG Galaxy local state | Research | Read-only investigation; no Galaxy database writes. |

## Support criteria

A format is considered supported only when:

- detection is deterministic;
- fixtures cover locked and unlocked states;
- malformed or partial writes fail safely;
- timestamps and progress are represented honestly;
- automated tests verify normalization;
- diagnostics identify the selected adapter and evidence path.

The project will not bundle source runtimes, platform DLLs, account credentials, or game files.
