# Manual intent timeline

> **Tester / product-owner tooling.** Hotkeys + Timeline is internal scenario-discovery tooling. It is **not** a normal end-user feature, and it is **not** an automatic music engine. It only runs when the host is started in **intent-capture runtime mode** (see below).

This document describes the **Manual Intent Timeline** feature: a single ordered log that combines **normalized GSI gameplay events** and **manual user music actions**, with **flattened game context** attached to each manual entry. The purpose is data collection and future analysis (for example pattern detection or suggested rules), not automated scenario generation in this slice.

## Runtime modes

The host has two explicit runtime modes (`Runtime:Mode` in `appsettings.json`):


| Mode                | Default?    | What it does                                                                                                                                                                                                                                                                                                                              |
| ------------------- | ----------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `scenario_playback` | **Yes**     | Normal end-user mode. GSI-driven scenario rules control Spotify. `/timeline`, `/timeline/episodes`, `/user-actions`, and Windows hotkeys are **not registered**. Tester-only services exist in DI but no endpoint reaches them.                                                                                                           |
| `intent_capture`    | No (opt-in) | Tester/product-owner mode. GSI is still ingested for context and recent event history, but `RulesEngine.DetectAsync` runs **without executing actions** so manual intent data is not polluted by automatic Spotify side effects. `/timeline`, `/timeline/episodes`, `/user-actions` are mapped, and `WindowsHotkeyService` is registered. |


### How to start in intent-capture mode

Either pass the CLI flag:

```powershell
dotnet run --project .\GsiHost -- --intent-capture
```

…or set it in `appsettings.json` / a local override:

```json
{
  "Runtime": { "Mode": "intent_capture" },
  "Timeline": { "Enabled": true },
  "ManualMusicActions": { "Enabled": true },
  "Keybinds": { "Enabled": true }
}
```

Both `Runtime:Mode = intent_capture` **and** the per-feature `Enabled` flag must be true for a tester service to act. The product default is everything off.

## Goals

- Record what happened in the game (`round_start`, `death`, etc.) and what the user did manually (pause, duck, resume, …) in **one timeline**.
- Attach **current game context** to every manual action (alive, health, round phase, recent event keys, etc.).
- Support **intent episodes**: a bounded window of timeline entries **before** and **after** each manual action, for future training or prompt-style use.
- In `intent_capture` mode, GSI events are **detected but not auto-acted on**, so captured intent data reflects the tester's choices, not the rules engine.

## What is not in scope here

- No LLM calls, no automatic rule generation.
- Manual actions do **not** create `NormalizedEvent` instances and do **not** go through `RulesEngine.ActionMap`.
- Hotkeys + Timeline does not appear in any client UI; it is HTTP-only and CLI-only.

## Runtime flow (intent capture mode)

1. **GSI** — `POST /gsi` → `GsiProcessingService` → `RulesEngine.DetectAsync` (diff, detect, **no** action execution).
2. **Timeline** — `TimelineCaptureService` subscribes to `GsiProcessingService.Processed`. On each processed payload it updates the latest `TimelineGameContext` and appends one entry per emitted normalized event (`source: gsi`).
3. **Manual actions** — `POST /user-actions` → `UserActionService`:
  - validates the request (`custom:*` event-key requirement, optional allowlist),
  - resolves the active rule in `control-profiles.json` by **event key** (e.g. `custom:music_pause`),
  - calls `ISpotifyPlaybackControl` directly (pause, resume, duck, restore),
  - records a timeline entry (`source: user_action`) with outcome (`applied`, `no_matching_rule`, `invalid`, `disabled`, `failed`).

`TimelineCaptureService` is resolved **eagerly** at host startup so the `Processed` subscription is always active in intent-capture mode.

## HTTP API (intent capture mode only)


| Method | Path                 | Purpose                                                                                              |
| ------ | -------------------- | ---------------------------------------------------------------------------------------------------- |
| `GET`  | `/timeline`          | Recent in-memory timeline entries (ordered by `sequence`).                                           |
| `GET`  | `/timeline/episodes` | Manual-intent **episodes**: each manual entry as `label` plus `before` / `after` windows of entries. |
| `POST` | `/user-actions`      | Record a manual music intent and optionally apply matching control-profile command.                  |


All three return `**404 Not Found`** in `scenario_playback` mode — they are not even mapped.

### `POST /user-actions` body

Minimal JSON:

```json
{
  "eventKey": "custom:music_pause",
  "action": "pause",
  "detail": "optional free text"
}
```

- `**eventKey**` (required): **must** be in the `custom:` namespace (e.g. `custom:music_pause`). Non-custom keys such as `round_start` or `death` are rejected with status `invalid`. This keeps the manual-action path strictly separated from GSI-driven `SpotifyControlProfileAction` so a tester's manual command cannot accidentally fire a scenario rule.
- `**action`** (optional): informational label in the timeline; the **command executed** comes from `control-profiles.json` (`pause`, `resume`, `duck`, `restore_volume`).
- `**detail`** (optional): origin hint (`hotkey:…`, script name, etc.).

Response shape: `{ "entry": { … }, "outcome": { "status": "…", "command": "…", "message": "…" } }` (no secrets).

### `POST /gsi/reset`

Available in both modes (gated by `Gsi:AllowReset`). In intent-capture mode it also clears the in-memory timeline and starts a **new** append-only JSONL session file under the configured timeline directory. Use this as a **session boundary** (for example after a match or when restarting the simulator).

## Configuration (`appsettings.json`)

Defaults for tester-only sections are **off**. Each section also requires `Runtime:Mode = intent_capture` to actually do anything:

### `Runtime`


| Property | Purpose                                                                                                                |
| -------- | ---------------------------------------------------------------------------------------------------------------------- |
| `Mode`   | `scenario_playback` (default) or `intent_capture`. CLI flags `--intent-capture` / `--scenario-playback` override this. |


### `Timeline`


| Property                  | Purpose                                                                                                                                |
| ------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| `Enabled`                 | When false (default), no timeline entries are appended to memory or disk. Even when true, has no effect outside `intent_capture` mode. |
| `MaxInMemoryEntries`      | Ring buffer size for `GET /timeline`.                                                                                                  |
| `Directory`               | Relative to host content root, or absolute path; JSONL files live here.                                                                |
| `EpisodeBeforeEntryCount` | Max entries before each manual action in `/timeline/episodes`.                                                                         |
| `EpisodeAfterEntryCount`  | Max entries after each manual action in `/timeline/episodes`.                                                                          |


Session files are named like `session-{yyyyMMdd-HHmmss-fff}-{guid}.jsonl` (append-only, one JSON object per line).

### `ManualMusicActions`


| Property           | Purpose                                                                                                                                      |
| ------------------ | -------------------------------------------------------------------------------------------------------------------------------------------- |
| `Enabled`          | When false (default), `POST /user-actions` records a `disabled` outcome and does not call Spotify.                                           |
| `AllowedEventKeys` | Empty array = allow any `custom:*` event key. Non-empty = only listed `custom:*` keys (case-insensitive) are accepted; others get `invalid`. |


### `Keybinds` (Windows-only, intent-capture only)


| Property   | Purpose                                                                                                                |
| ---------- | ---------------------------------------------------------------------------------------------------------------------- |
| `Enabled`  | When true on Windows in intent-capture mode, registers global hotkeys that call the same path as `POST /user-actions`. |
| `Bindings` | List of `{ "Key", "EventKey", "Action", "Detail" }`. `EventKey` must be in the `custom:` namespace.                    |


**Key string format** (examples): `Ctrl+Alt+M`, `Shift+F9`. Modifiers: `Ctrl`, `Alt`, `Shift`, `Win`. Single-letter keys and `F1`–`F24` and a few named keys (`Space`, `Esc`, arrows) are supported; see `GsiHost/Services/WindowsHotkeyService.cs` for parsing details.

In `scenario_playback` mode the hotkey hosted service is **not registered** at all, so no message loop runs even if `Keybinds:Enabled` were true.

## Control profiles and `custom:*` keys

Manual intents reuse `**control-profiles.json`** for command resolution: add rules whose `eventKey` is a stable string in the `custom:` namespace, such as `custom:music_pause` → `pause`. The default profile created by `JsonControlProfileService` includes sample rules for mute, pause, resume, and restore.

**Important:** `UserActionService` only accepts `custom:`* event keys. GSI-driven `SpotifyControlProfileAction` continues to use `RulesEngine.ActionMap` for normalized event keys like `round_start` or `death`. The two paths cannot share a row by accident.

A future issue (UND-27) tracks the longer-term option of moving manual-action mapping into its own `manual-actions.json` file. Until then, the `custom:` namespace check is the boundary.

## Data model

Types live in `GsiHost/Tooling/Timeline/TimelineModels.cs` (namespace `GsiHost.Tooling.Timeline`). They intentionally live in `GsiHost`, not `Core`, because tester tooling is not part of the shared model layer.

- `TimelineEntry` — `sequence`, `timestampUtc`, `source` (`gsi` | `user_action`), `eventKey`, optional `action`/`detail`, `gameContext`, optional `outcome`.
- `TimelineGameContext` — flattened snapshot fields plus `recentEventKeys`.
- `IntentEpisode` — `label` (the manual `TimelineEntry`) plus `before` / `after` entry lists.

Outcome `status` values: `applied`, `no_matching_rule`, `invalid`, `disabled`, `failed` (and `received` reserved for future use).

## Code pointers

- Mode resolution: `GsiHost/Configuration/RuntimeOptions.cs`
- Host routes (gated): `GsiHost/Program.cs`
- GSI → timeline: `GsiHost/Services/TimelineCaptureService.cs`
- Manual apply + record: `GsiHost/Services/UserActionService.cs`
- Reset wiring: `GsiHost/Services/GsiResetService.cs`
- Hotkeys: `GsiHost/Services/WindowsHotkeyService.cs`

## Related docs

- [Rules engine migration](rules-engine-migration.md) — how this relates to a single orchestration entry for **GSI ticks** vs manual side path.
- [Backend architecture](backend-architecture.md) — full HTTP surface and pipeline overview.

