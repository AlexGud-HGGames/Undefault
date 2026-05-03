# Manual intent timeline

This document describes the **Manual Intent Timeline** feature: a single ordered log that combines **normalized GSI gameplay events** and **manual user music actions**, with **flattened game context** attached to each manual entry. The purpose is data collection and future analysis (for example pattern detection or suggested rules), not automated scenario generation in this slice.

## Goals

- Record what happened in the game (`round_start`, `death`, etc.) and what the user did manually (pause, duck, resume, …) in **one timeline**.
- Attach **current game context** to every manual action (alive, health, round phase, recent event keys, etc.).
- Support **intent episodes**: a bounded window of timeline entries **before** and **after** each manual action, for future training or prompt-style use.
- Keep **GSI-driven music** on the existing path: `RulesEngine.ActionMap` → `spotify.control_profile` for detector events only.

## What is not in scope here

- No LLM calls, no automatic rule generation.
- Manual actions do **not** create `NormalizedEvent` instances and do **not** go through `RulesEngine.ActionMap`.

## Runtime flow

1. **GSI** — `POST /gsi` → `GsiProcessingService` → `RulesEngine` (diff, detect, run `ActionMap` actions).
2. **Timeline** — `TimelineCaptureService` subscribes to `GsiProcessingService.Processed`. On each processed payload it updates the latest `TimelineGameContext` and appends one entry per emitted normalized event (`source: gsi`).
3. **Manual actions** — `POST /user-actions` → `UserActionService`:
   - validates the request (including optional allowlist),
   - resolves the active rule in `control-profiles.json` by **event key** (same as console profiles, including `custom:*` keys),
   - calls `ISpotifyPlaybackControl` directly (pause, resume, duck, restore),
   - records a timeline entry (`source: user_action`) with outcome (`applied`, `no_matching_rule`, `invalid`, `disabled`, `failed`).

`TimelineCaptureService` is resolved **eagerly** at host startup (same pattern as `AppStateService`) so the `Processed` subscription is always active.

## HTTP API

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/timeline` | Recent in-memory timeline entries (ordered by `sequence`). |
| `GET` | `/timeline/episodes` | Manual-intent **episodes**: each manual entry as `label` plus `before` / `after` windows of entries. |
| `POST` | `/user-actions` | Record a manual music intent and optionally apply matching control-profile command. |

### `POST /user-actions` body

Minimal JSON:

```json
{
  "eventKey": "custom:music_pause",
  "action": "pause",
  "detail": "optional free text"
}
```

- **`eventKey`** (required): must match a rule in the active console control profile, e.g. `custom:music_pause`. Normalized keys like `round_start` can be used only if you add explicit rules; they are still **not** routed through `RulesEngine` for this endpoint.
- **`action`** (optional): informational label in the timeline; the **command executed** comes from `control-profiles.json` (`pause`, `resume`, `duck`, `restore_volume`).
- **`detail`** (optional): origin hint (`hotkey:…`, script name, etc.).

Response shape: `{ "entry": { … }, "outcome": { "status": "…", "command": "…", "message": "…" } }` (no secrets).

### `POST /gsi/reset`

Clears detector/snapshot state, recent `/events` ring, **and** the in-memory timeline; starts a **new** append-only JSONL session file under the configured timeline directory. Use this as a **session boundary** (for example after a match or when restarting the simulator).

## Configuration (`appsettings.json`)

### `Timeline`

| Property | Purpose |
|----------|---------|
| `Enabled` | When false, timeline entries are not appended to memory or disk. |
| `MaxInMemoryEntries` | Ring buffer size for `GET /timeline`. |
| `Directory` | Relative to host content root, or absolute path; JSONL files live here. |
| `EpisodeBeforeEntryCount` | Max entries before each manual action in `/timeline/episodes`. |
| `EpisodeAfterEntryCount` | Max entries after each manual action in `/timeline/episodes`. |

Session files are named like `session-{yyyyMMdd-HHmmss-fff}-{guid}.jsonl` (append-only, one JSON object per line).

### `ManualMusicActions`

| Property | Purpose |
|----------|---------|
| `Enabled` | When false, `POST /user-actions` records a `disabled` outcome and does not call Spotify. |
| `AllowedEventKeys` | Empty array = allow any non-empty event key. Non-empty = only listed keys (case-insensitive) are accepted; others get `invalid`. |

### `Keybinds` (optional, Windows)

| Property | Purpose |
|----------|---------|
| `Enabled` | When true on Windows, registers global hotkeys that call the same path as `POST /user-actions`. |
| `Bindings` | List of `{ "Key", "EventKey", "Action", "Detail" }`. |

**Key string format** (examples): `Ctrl+Alt+M`, `Shift+F9`. Modifiers: `Ctrl`, `Alt`, `Shift`, `Win`. Single-letter keys and `F1`–`F24` and a few named keys (`Space`, `Esc`, arrows) are supported; see `GsiHost/Services/WindowsHotkeyService.cs` for parsing details.

Default sample bindings in repo `appsettings.json` are **disabled** (`Keybinds:Enabled: false`) so tests and headless runs do not start a message loop.

## Control profiles and `custom:*` keys

Manual intents reuse **`control-profiles.json`**: add rules whose `eventKey` is a stable string such as `custom:music_pause` → `pause`. The default profile created by `JsonControlProfileService` includes sample rules for mute, pause, resume, and restore.

**Important:** GSI events still use `RulesEngine.ActionMap` to decide whether `spotify.control_profile` (or other actions) run. Manual actions **do not** use `ActionMap`; they only need a matching row in the active control profile.

## Data model (Core)

Types live in `Core/Models/TimelineModels.cs`:

- `TimelineEntry` — `sequence`, `timestampUtc`, `source` (`gsi` | `user_action`), `eventKey`, optional `action`/`detail`, `gameContext`, optional `outcome`.
- `TimelineGameContext` — flattened snapshot fields plus `recentEventKeys`.
- `IntentEpisode` — `label` (the manual `TimelineEntry`) plus `before` / `after` entry lists.

Outcome `status` values: `applied`, `no_matching_rule`, `invalid`, `disabled`, `failed` (and `received` reserved for future use).

## Code pointers

- Host routes: `GsiHost/Program.cs`
- GSI → timeline: `GsiHost/Services/TimelineCaptureService.cs`
- Manual apply + record: `GsiHost/Services/UserActionService.cs`
- Reset wiring: `GsiHost/Services/GsiResetService.cs`
- Hotkeys: `GsiHost/Services/WindowsHotkeyService.cs`

## Related docs

- [Rules engine migration](rules-engine-migration.md) — how this relates to a single orchestration entry for **GSI ticks** vs manual side path.
- [Backend architecture](backend-architecture.md) — full HTTP surface and pipeline overview.
