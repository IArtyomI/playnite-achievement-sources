# ADR 0001: Build an independent source plugin

- Status: Accepted
- Date: 2026-07-28

## Context

Playnite Achievements already provides a mature display and statistics system, but its provider registry is internal to that extension. Replacing the standard extension with a permanent fork would couple local source work to unrelated presentation features and upstream maintenance.

## Decision

Build Achievement Sources as an independent Playnite plugin with its own extension ID, storage, source adapters, diagnostics, and per-game policies.

Pursue compatibility through a small, generic, versioned bridge. A temporary fork may be used to prototype that bridge, but it is not the intended distributed product.

## Consequences

Advantages:

- the standard Playnite Achievements extension remains independently installable and updateable;
- source-specific logic stays isolated;
- licensing and authorship remain clear;
- local providers can evolve without duplicating presentation code;
- other display extensions may consume the same export format.

Tradeoffs:

- native Playnite Achievements integration requires cooperation or a generic upstream bridge;
- the plugin needs a minimal diagnostic view before that integration exists;
- versioning and compatibility rules must be defined carefully.
