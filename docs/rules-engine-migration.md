# Rules engine migration — single orchestration entry

## Problem

Running both `RulesEngine` (`ActionMap` → `IEventAction`) and a new **music session / safety** path in the same tick can **double-apply** Spotify side effects.

## Rule (non-negotiable)

**Exactly one** orchestration entry applies playback side effects per evaluation tick.

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

## Checklist before removing legacy actions

- [ ] Golden tests for mixer + safety transitions
- [ ] Integration test: one GSI POST does not double Spotify calls (mock client call count)
