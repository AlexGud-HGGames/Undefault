# Agent notes

Internal constraints for Cursor / coding agents working on this repo. Not part of the public portfolio pitch — see [README.md](README.md) for the product overview.

## Project

- `UndefaultIt` is a Windows-first local .NET backend for gameplay-driven Spotify control.
- Current runtime focus: `CS2` + `GsiHost`.
- Current default behavior: `round_start` / `death` → `spotify.control_profile`.

## Modules

- `Core/` — models, diffing, detection, rules, Spotify abstractions, `Core/Music/` contracts.
- `GsiHost/` — HTTP host, GSI mapping, processing pipeline, config, OAuth, CS2 setup.
- `Cs2Simulator/`, `Cs2Simulator.Runtime/`, `Cs2Simulator.Scenarios/` — local CS2 GSI simulator; see [docs/cs2-simulator.md](docs/cs2-simulator.md).
- `Core.Tests/`, `GsiHost.Tests/`, `Cs2Simulator.Tests/` — unit and integration coverage.

## Runtime flow

`CS2 GSI` → `POST /gsi` → `GsiProcessingService` → `GsiSnapshotMapper` → `GameSnapshot` → `SnapshotDiffer` → `EventDetector` → `RulesEngine` → `IEventAction`

## Constraints

- No YAML scenario engine.
- No full Dota 2 runtime support yet: `POST /gsi/dota` only logs events to the timeline (UND-80); there is no `DotaGameAdapter`, no neutral-context mapping, and no Spotify actions triggered by Dota events (tracked in UND-45).
- Default Spotify control path: `spotify.control_profile` + `GsiHost/control-profiles.json`.
- Real Spotify control requires Premium and an active playback device.
- Spotify features must stay inside the [local playback control boundary](docs/spotify-playback-policy-boundary.md).
- Safety-first music architecture is documented; runtime integration is partial.
- Prefer Linear as the source of truth for scope; do not invent product decisions.

## Read first

- [docs/README.md](docs/README.md)
- [docs/backend-architecture.md](docs/backend-architecture.md)
- [docs/quick-launch.md](docs/quick-launch.md)
- [docs/music-safety-state-spec.md](docs/music-safety-state-spec.md)
- [docs/failure-safety-spec.md](docs/failure-safety-spec.md)
- [docs/volume-composition-spec.md](docs/volume-composition-spec.md)
- [docs/stability-and-device-layer-spec.md](docs/stability-and-device-layer-spec.md)
- [docs/neutral-signals-and-game-clock.md](docs/neutral-signals-and-game-clock.md)
- [docs/ingestion-spec-cs2-dota.md](docs/ingestion-spec-cs2-dota.md)
- [docs/rules-engine-migration.md](docs/rules-engine-migration.md)
- [docs/manual-intent-timeline.md](docs/manual-intent-timeline.md)
- [docs/roadmap.md](docs/roadmap.md)
