# Product pivot — 2026-08-14

Locked product-owner decision. `PIVOT-1`–`PIVOT-8` are implemented in-repo. This document remains the source of truth for product direction. Older MVP notes (UND-64 `intent_capture`, Spotify-required) are historical.

## What Undefault is

Undefault is a **game-aware music automation layer**. It reads game state, detects events, and tells the user's existing music player what to do.

Undefault owns:

- game integrations
- event detection
- rules
- actions
- playback orchestration

Undefault does **not** own the music catalog.

## What Undefault is not

- a Spotify controller
- a synchronized game soundtrack
- a music player
- a recommendation or playlist product

## First playback backend

**Tauon Music Box**, via its remote HTTP API. Undefault and Tauon stay independent projects. Do not fork or patch Tauon.

Spotify is **not** a product backend. Playback control via the Web API is a Streaming SDA; game-adjacent use, synchronization with visuals, and commercial Streaming SDAs are restricted. Development Mode is a 5-user tinkering sandbox, not a ship path. See [spotify-constraints.md](spotify-constraints.md). Leftover Spotify code is to be removed after Tauon works, not wrapped as a user-facing provider.

## Approved MVP loop

```text
CS2 round_start → resume music
CS2 death       → pause music
```

That replaces the previous default `round_start → duck` / `death → restore_volume`.

Not in this MVP: `round_end`, victory/defeat tracks, playlist/queue/track-id playback, live safety mixer, Dota automation, UI, packaging.

## Current code vs this decision

`PIVOT-1`–`PIVOT-8` are in-repo. Live automation is `music.control_profile` with `round_start → resume` / `death → pause` through `IMusicPlayer` (Tauon default, Mock for `--quick`). Leftover Spotify types remain until `PIVOT-10`. Live Tauon smoke is `PIVOT-9`.

## Linear

This repo still prefers Linear as the issue tracker. The Cursor Linear MCP in this workspace currently points at **Counterplay**, not Undefault. Until an Undefault Linear project is connected, the in-repo roadmap IDs (`PIVOT-*`) are the working backlog.
