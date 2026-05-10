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

## Legacy detector phase strings (Phase 2 carry-over)

`EventDetectorOptions.RoundStartPhase` (default `"live"`) is still a CS2-only string that lives on `EventDetector`. It remains the legacy compatibility path so the existing `round_start -> duck` and `death -> restore_volume` behavior stays byte-for-byte identical while `Cs2GameAdapter` (UND-21 / Phase 2) populates neutral `MatchPhaseNeutral`, `SafetyFacts`, and `NeutralContext`.

Removing the CS2 phase string from `EventDetector` is intentionally deferred:

- the music safety + session controller is not yet wired (`IMusicOrchestrationFacade`, UND-22 / Phase 3);
- migrating detection to consume `MatchPhaseNeutral.Live` instead of the raw CS2 string would change `EventDetector`’s contract before there is a neutral consumer to absorb the change.

A later phase will move `round_start` detection (or its replacement event) onto the neutral clock and drop the CS2 phase string from detector options.

## Manual intent timeline (current implementation)

**Manual music actions** (`POST /user-actions`, optional Windows hotkeys) apply `control-profiles.json` by calling `ISpotifyPlaybackControl` directly. They **do not** enqueue `NormalizedEvent` values and **do not** consult `RulesEngine.ActionMap`.

That is intentional for this MVP: one orchestration entry still applies **per GSI evaluation tick** via `RulesEngine`. Manual commands are a separate user-driven path that must not double-fire the same detector-driven actions. When a future `IMusicOrchestrationFacade` exists, manual actions should be folded into that single entry rather than growing parallel Spotify call sites.

Risk callout: `UserActionService` and timeline/hotkey intent capture remain separate tester/product-owner tooling today. A future facade must absorb that path before it becomes an automated music controller path; otherwise manual commands and GSI-driven decisions can grow into parallel Spotify writers again.

## Checklist before removing legacy actions

- [x] Baseline integration test: one GSI `round_start` does not double Spotify calls (mock client call count) — `GsiHostIntegrationTests.GsiEndpoint_ShadowMode_RoundStartTick_LegacyDucksOnce_AndShadowReportsSafe` and `GsiEndpoint_ShadowMode_DeathTick_LegacyRestoresOnce_AndShadowReportsDanger` (UND-22).
- [x] Manual intent isolation: `POST /user-actions` does not invoke the facade and does not route through `RulesEngine.ActionMap` — `GsiHostIntegrationTests.IntentCapture_UserAction_DoesNotInvokeFacade_OrRouteThroughActionMap` (UND-22).
- [ ] Golden tests for mixer + safety transitions (Phase B prerequisite).
