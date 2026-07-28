# Architecture

## Boundary

Achievement Sources is an independent Playnite generic plugin. It does not replace Playnite Achievements and does not modify another extension's files or database.

The plugin owns:

- source and adapter discovery;
- per-game tracking policy;
- normalized achievement records;
- local source monitoring;
- source diagnostics;
- conflict and evidence preservation;
- versioned integration exports.

A display extension may consume normalized exports and decide how to present achievements, notifications, statistics, screenshots, or theme elements.

## Processing pipeline

1. Receive a Playnite application or game lifecycle event.
2. Resolve the effective per-game tracking mode.
3. Discover capable achievement sources.
4. Read source data without modifying the source.
5. Normalize records into the internal model.
6. Merge records while preserving source evidence.
7. Persist the normalized snapshot in plugin-owned storage.
8. Publish a versioned export for approved integrations.

## Source adapters

Every source implements `IAchievementSource` and must:

- identify itself with a stable key;
- declare capability without performing expensive work;
- honor cancellation;
- return diagnostics instead of silently swallowing unsupported states;
- distinguish a complete snapshot from partial evidence;
- avoid changing game, platform, or third-party extension files.

## Storage

The plugin will maintain its own storage under Playnite's plugin user-data directory. Source files remain authoritative evidence for new observations, while plugin-owned storage preserves normalized history and protects against temporary provider failures.

The initial foundation intentionally does not select a database implementation. SQLite is the likely choice once persistence requirements and Playnite dependency compatibility are verified.

## Integration

The preferred long-term integration is a narrow, versioned contract consumed by Playnite Achievements or another display extension. Direct access to another plugin's private database is out of scope.
