# Manual intent timeline

This document describes the **Manual Intent Timeline** feature: a tester/product-owner intent-capture mode that combines **normalized GSI gameplay events** and **manual music actions** into one ordered log, with **flattened game context** attached to each manual entry. The purpose is data collection and future analysis (for example pattern detection or suggested rules), not automated scenario generation in this slice.

## Goals

- Record what happened in the game (`round_start`, `death`, etc.) and what the user did manually (pause, duck, resume, …) in **one timeline**.
- Attach **current game context** to every manual action (alive, health, round phase, recent event keys, etc.).
- Support **intent episodes**: a bounded window of timeline entries **before** and **after** each manual action, for future training or prompt-style use.
- Keep normal **GSI-driven music** on the scenario playback path: `RulesEngine.ActionMap` → `spotify.control_profile` for detector events only.

## What is not in scope here

- No LLM calls, no automatic rule generation.
- Manual actions do **not** create `NormalizedEvent` instances and do **not** go through `RulesEngine.ActionMap`.

## Runtime flow

The host has an explicit `Runtime:Mode` switch:

- `scenario_playback` is the normal user mode. GSI payloads run the rules engine and `RulesEngine.ActionMap` can apply Spotify actions. Tester-only `/timeline`, `/timeline/episodes`, `/user-actions`, and hotkeys are unavailable by default.
- `intent_capture` is tester/product-owner mode. GSI payloads are diffed and detected so current context and recent normalized events can be captured, but automatic scenario playback actions are skipped to avoid polluting manual intent data.

In `intent_capture`:

1. **GSI** — `POST /gsi` → `GsiProcessingService` → `RulesEngine.DetectAsync` (diff and detect only; no `ActionMap` dispatch).
2. **Timeline** — `TimelineCaptureService` subscribes to `GsiProcessingService.Processed`. On each processed payload it updates the latest `TimelineGameContext` and appends one entry per emitted normalized event (`source: gsi`).
3. **Manual actions** — `POST /user-actions` → `UserActionService`:
   - validates the request (including optional allowlist),
   - resolves a separate `ManualMusicActions:CommandMappings` entry by **event key**,
   - calls `ISpotifyPlaybackControl` directly (pause, resume, duck, restore),
   - records a timeline entry (`source: user_action`) with outcome (`applied`, `no_matching_rule`, `invalid`, `disabled`, `failed`).

`TimelineCaptureService` is resolved **eagerly** only when timeline capture is effectively enabled, so normal scenario playback does not write timeline JSONL files unless explicitly configured as diagnostics.

## HTTP API

These endpoints are intent-capture only. In `scenario_playback` they are not mapped.

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/timeline` | Recent in-memory timeline entries (ordered by `sequence`). |
| `GET` | `/timeline/episodes` | Manual-intent **episodes**: each manual entry as `label` plus `before` / `after` windows of entries. |
| `POST` | `/user-actions` | Record a manual music intent and optionally apply matching manual command mapping. |

### `POST /user-actions` body

Minimal JSON:

```json
{
  "eventKey": "custom:music_pause",
  "action": "pause",
  "detail": "optional free text"
}
```

- **`eventKey`** (required): must match a `ManualMusicActions:CommandMappings` entry, e.g. `custom:music_pause`. Normalized keys like `round_start` can be used only if you add explicit manual mappings; they are still **not** routed through `RulesEngine` for this endpoint.
- **`action`** (optional): informational label in the timeline; the **command executed** comes from `ManualMusicActions:CommandMappings` (`pause`, `resume`, `duck`, `restore_volume`).
- **`detail`** (optional): origin hint (`hotkey:…`, script name, etc.).

Response shape: `{ "entry": { … }, "outcome": { "status": "…", "command": "…", "message": "…" } }` (no secrets).

### `POST /gsi/reset`

Clears detector/snapshot state, recent `/events` ring, **and** the in-memory timeline; starts a **new** append-only JSONL session file under the configured timeline directory. Use this as a **session boundary** (for example after a match or when restarting the simulator).

## Configuration (`appsettings.json`)

### `Runtime`

| Property | Purpose |
|----------|---------|
| `Mode` | `scenario_playback` for normal GSI-driven playback, or `intent_capture` for tester-only capture. CLI flags `--scenario-playback` and `--intent-capture` override this for a run. |

### `Timeline`

| Property | Purpose |
|----------|---------|
| `Enabled` | Optional override. When omitted, enabled only in `intent_capture`. When false, timeline entries are not appended to memory or disk. |
| `MaxInMemoryEntries` | Ring buffer size for `GET /timeline`. |
| `Directory` | Relative to host content root, or absolute path; JSONL files live here. |
| `EpisodeBeforeEntryCount` | Max entries before each manual action in `/timeline/episodes`. |
| `EpisodeAfterEntryCount` | Max entries after each manual action in `/timeline/episodes`. |

Session files are named like `session-{yyyyMMdd-HHmmss-fff}-{guid}.jsonl` (append-only, one JSON object per line).

### `ManualMusicActions`

| Property | Purpose |
|----------|---------|
| `Enabled` | Optional override. When omitted, enabled only in `intent_capture`. When false, `POST /user-actions` records a `disabled` outcome and does not call Spotify. |
| `AllowedEventKeys` | Empty array = allow any non-empty event key. Non-empty = only listed keys (case-insensitive) are accepted; others get `invalid`. |
| `CommandMappings` | Separate manual action mapping list: `{ "EventKey", "Command", "VolumePercent" }`. This replaces the old reuse of `control-profiles.json` for manual intent capture. |

### `Keybinds` (optional, Windows)

| Property | Purpose |
|----------|---------|
| `Enabled` | Optional override. When omitted, enabled only in `intent_capture`. When true on Windows, registers global hotkeys that call the same path as `POST /user-actions`. |
| `Bindings` | List of `{ "Key", "EventKey", "Action", "Detail" }`. |

**Key string format** (examples): `Ctrl+Alt+M`, `Shift+F9`. Modifiers: `Ctrl`, `Alt`, `Shift`, `Win`. Single-letter keys and `F1`–`F24` and a few named keys (`Space`, `Esc`, arrows) are supported; see `GsiHost/Services/WindowsHotkeyService.cs` for parsing details.

Default sample bindings in repo `appsettings.json` are inert in `scenario_playback`; switching to `intent_capture` enables them unless `Keybinds:Enabled` is explicitly set to false.

## Manual command mappings and `custom:*` keys

Manual intents use **`ManualMusicActions:CommandMappings`**: add mappings whose `EventKey` is a stable string such as `custom:music_pause` → `pause`. The default manual mapping includes mute, pause, resume, and restore.

**Important:** GSI scenario playback still uses `RulesEngine.ActionMap` to decide whether `spotify.control_profile` (or other actions) run. Manual actions **do not** use `ActionMap` or `control-profiles.json`; they only need a matching manual command mapping.

## Data model (GsiHost)

Types live in `GsiHost/Models/TimelineModels.cs` because timeline capture is tester-only host tooling:

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
