# Rules engine migration — single orchestration entry

## Problem

Running both `RulesEngine` (`ActionMap` → `IEventAction`) and a new **music session / safety** path in the same tick can **double-apply** Spotify side effects.

## Rule (non-negotiable)

**Exactly one** orchestration entry applies playback side effects per evaluation tick.

## Adapter boundary (precondition)

GSI ticks now cross a title adapter before legacy rule evaluation:

`GsiProcessingService` -> `IGameAdapter<GsiPayloadDto>` -> `AdapterObservation`.

`AdapterObservation.Raw` preserves the existing `GameSnapshot` path for `RulesEngine`.
`AdapterObservation.Clock`, `NeutralContext`, `SafetyFacts`, and title domain events are the handoff for Phase A facade work. The facade should consume those adapter outputs instead of re-parsing CS2 DTOs or reading CS2-only module strings directly.

## Phased approach

### Phase A — Facade behind existing host

- Introduce `IMusicOrchestrationFacade` (name TBD) invoked from `GsiProcessingService` **after** or **instead of** parts of rules evaluation for music keys only.
- `RulesEngine` either:
  - **Delegates** music-related action keys to the facade (actions become no-ops for those keys), or
  - Feature flag **`UseLegacyRulesOnly`** routes entire tick (dev rollback).

#### Phase A status (UND-22)

The shadow-mode side of Phase A is now wired:

- `Core/Music/IMusicOrchestrationFacade.cs` exposes `EvaluateShadow(AdapterObservation)` returning a `MusicEngineDebugSnapshot`.
- `Core/Music/ShadowMusicOrchestrationFacade.cs` is the default implementation. It is deterministic and side-effect free: no Spotify calls, no detector mutation, no `RulesEngine.ActionMap` changes.
- `GsiHost/Services/GsiProcessingService.cs` invokes the facade after the existing `RulesEngine.EvaluateAsync` / `DetectAsync` call. The shadow call is synchronous and guarded by `try/catch`; a throw is logged and ignored so the legacy path is never broken.
- Output flows into `IShadowMusicSnapshotSink` (default `InMemoryShadowMusicSnapshotSink`, bounded to 32 entries) and is exposed read-only at `GET /diagnostics/music-shadow`. This endpoint is **debug/observability surface only** — it is intentionally not user-facing product surface, and is mapped in both runtime modes for the migration window.
- `appsettings.json` adds `MusicOrchestration:ShadowMode` (default `true`). Setting it to `false` skips the facade call and leaves the diagnostics endpoint returning `{ latest: null, recent: [] }`.

`RulesEngine.ActionMap` is **not** shrunk yet — `round_start -> spotify.control_profile` and `death -> spotify.control_profile` continue to drive Spotify side effects. Phase B will compare facade output with legacy outcomes, then remove the music keys from `ActionMap` and let the facade apply playback.

### Phase B — Shrink `ActionMap`

- Remove `spotify.control_profile` / music keys from `ActionMap` once facade parity is proven.
- Keep `LogEventAction` or diagnostics if still needed.

### Phase C — Deprecation

- `ActionMap` empty for music or removed; detectors may still emit events consumed by the facade.

## Naming note

The manifesto referenced “ScenarioController”; that feature was removed. The migration target is **music safety + session controller**, not YAML scenarios.

## Detector consumes neutral signals (UND-39)

`EventDetector` now reads only neutral signals from `AdapterObservation`:

- `round_start` fires on a `MatchPhaseNeutral.Live` transition (non-Live → Live) **or** on a `GameClockSnapshot.RoundIndex` increment. CS2 phase strings no longer appear in detector code; `EventDetectorOptions.RoundStartPhase` was removed.
- `death` fires on a `NeutralContext.IsAlive` transition `true → false`. A `null → false` transition is intentionally **not** a death event (unknown alive state cannot be proven to be a transition).
- The detector signature is `Detect(NeutralDetectorContext)`; `IRulesEngine.EvaluateAsync` / `DetectAsync` accept `AdapterObservation` directly.
- Combat / idle still consume `ActivityDiff` plus raw `CombatModule` / `PositionModule` reads through `AdapterObservation.Raw`. Neutralizing them is tracked separately and is not in scope of the round/death migration.

Event keys (`round_start`, `death`) are unchanged so `RulesEngine.ActionMap` continues to fire the same actions; renaming or namespacing keys (`cs2.round_start`, etc.) belongs to the scenario rule pack issue.

## Manual intent timeline (current implementation)

**Manual music actions** (`POST /user-actions`, optional Windows hotkeys) apply `control-profiles.json` by calling `ISpotifyPlaybackControl` directly. They **do not** enqueue `NormalizedEvent` values and **do not** consult `RulesEngine.ActionMap`.

That is intentional for this MVP: one orchestration entry still applies **per GSI evaluation tick** via `RulesEngine`. Manual commands are a separate user-driven path that must not double-fire the same detector-driven actions. When a future `IMusicOrchestrationFacade` exists, manual actions should be folded into that single entry rather than growing parallel Spotify call sites.

Risk callout: `UserActionService` and timeline/hotkey intent capture remain separate tester/product-owner tooling today. A future facade must absorb that path before it becomes an automated music controller path; otherwise manual commands and GSI-driven decisions can grow into parallel Spotify writers again.

## Checklist before removing legacy actions

- [x] Baseline integration test: one GSI `round_start` does not double Spotify calls (mock client call count) — `GsiHostIntegrationTests.GsiEndpoint_ShadowMode_RoundStartTick_LegacyDucksOnce_AndShadowReportsSafe` and `GsiEndpoint_ShadowMode_DeathTick_LegacyRestoresOnce_AndShadowReportsDanger` (UND-22).
- [x] Manual intent isolation: `POST /user-actions` does not invoke the facade and does not route through `RulesEngine.ActionMap` — `GsiHostIntegrationTests.IntentCapture_UserAction_DoesNotInvokeFacade_OrRouteThroughActionMap` (UND-22).
- [ ] Golden tests for mixer + safety transitions (Phase B prerequisite).
