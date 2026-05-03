# Rules engine migration — single orchestration entry

## Problem

Running both `RulesEngine` (`ActionMap` → `IEventAction`) and a new **music session / safety** path in the same tick can **double-apply** Spotify side effects.

## Rule (non-negotiable)

**Exactly one** orchestration entry applies playback side effects per evaluation tick.

## Adapter boundary (precondition)

GSI ticks now cross a title adapter before legacy rule evaluation:

`GsiProcessingService` → `IGameAdapter<GsiPayloadDto>` → `AdapterObservation`.

`AdapterObservation.Raw` preserves the existing `GameSnapshot` path for `RulesEngine`.
`AdapterObservation.Clock`, `NeutralContext`, `SafetyFacts`, and title domain events are the handoff for Phase A facade work. The facade should consume those adapter outputs instead of re-parsing CS2 DTOs or reading CS2-only module strings directly.

## Phased approach

### Phase A — Facade behind existing host

- Introduce `IMusicOrchestrationFacade` (name TBD) invoked from `GsiProcessingService` **after** or **instead of** parts of rules evaluation for music keys only.
- `RulesEngine` either:
  - **Delegates** music-related action keys to the facade (actions become no-ops for those keys), or
  - Feature flag **`UseLegacyRulesOnly`** routes entire tick (dev rollback).

### Phase B — Shrink `ActionMap`

- Remove `spotify.control_profile` / music keys from `ActionMap` once facade parity is proven.
- Keep `LogEventAction` or diagnostics if still needed.

### Phase C — Deprecation

- `ActionMap` empty for music or removed; detectors may still emit events consumed by the facade.

## Naming note

The manifesto referenced “ScenarioController”; that feature was removed. The migration target is **music safety + session controller**, not YAML scenarios.

## Manual intent timeline (current implementation)

**Manual music actions** (`POST /user-actions`, optional Windows hotkeys) apply `control-profiles.json` by calling `ISpotifyPlaybackControl` directly. They **do not** enqueue `NormalizedEvent` values and **do not** consult `RulesEngine.ActionMap`.

That is intentional for this MVP: one orchestration entry still applies **per GSI evaluation tick** via `RulesEngine`. Manual commands are a separate user-driven path that must not double-fire the same detector-driven actions. When a future `IMusicOrchestrationFacade` exists, manual actions should be folded into that single entry rather than growing parallel Spotify call sites.

Risk callout: `UserActionService` and timeline/hotkey intent capture remain separate tester/product-owner tooling today. A future facade must absorb that path before it becomes an automated music controller path; otherwise manual commands and GSI-driven decisions can grow into parallel Spotify writers again.

## Checklist before removing legacy actions

- [ ] Golden tests for mixer + safety transitions
- [ ] Baseline integration test: one GSI `round_start` does not double Spotify calls (mock client call count)
