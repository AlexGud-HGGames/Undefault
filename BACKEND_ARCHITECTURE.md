# Backend Architecture

## Scope

This document describes the current backend state of UndefaultIt. The backend is a small CS2-driven Spotify controller with a narrow MVP:

- ingest CS2 GSI payloads
- detect `round_start` and `death`
- apply a console-first music control profile on matching events
- duck Spotify volume on `round_start` in the default profile
- restore volume on `death` in the default profile
- auto-generate and install the CS2 GSI cfg
- support both real Spotify and mock Spotify modes

## Solution Shape

- `Core` holds the domain model, rules, actions, and Spotify abstractions.
- `GsiHost` hosts the ASP.NET Minimal API and infrastructure.
- `UI` is a separate Avalonia client that talks to the host over HTTP.

## Runtime Flow

```text
CS2 GSI POST
  -> /gsi
  -> GsiProcessingService
  -> GsiSnapshotMapper
  -> GameSnapshot
  -> SnapshotDiffer
  -> EventDetector
  -> RulesEngine
  -> IEventAction
  -> Spotify / logs
```

The current default console flow routes two built-in keys through the rules engine:

- `round_start` -> `spotify.control_profile`
- `death` -> `spotify.control_profile`

The default `GsiHost/control-profiles.json` file maps those events to:

- `round_start` -> `duck` with volume `0`
- `death` -> `restore_volume`

## Event Detection

`EventDetector` reads snapshot diffs and emits normalized events.

Current behavior:

- `round_start` is detected from the round module when the round increments or the phase becomes live
- `death` is detected when the player transitions from alive to dead
- `combat` and `idle` remain available in the detector, but are disabled by default

The detector is stateful and uses cooldown/debounce options from configuration.

## Spotify Volume Duck

`SpotifyControlProfileAction` is the current console-first playback action.

Supported control commands:

- `pause`
- `resume`
- `duck`
- `restore_volume`

Behavior:

- on a `duck` rule, it reads the current playback volume, stores it, and lowers Spotify to the configured target volume
- on a `restore_volume` rule, it restores the last saved volume
- on a `pause` rule, it pauses the current playback
- on a `resume` rule, it resumes the current playback
- if Spotify is not authenticated, it logs a warning and skips the action
- if there is no active playback device, it skips the action gracefully

Volume defaults still live in `SpotifyVolumeDuck`:

- `MuteVolume`
- `FallbackRestoreVolume`

`SpotifyVolumeDuckAction` still exists as a lower-level action, but the default backend path now goes through the control profile.

## Profiles And Rules

The rules engine maps event keys to action keys.

Current shape:

- `RulesEngineOptions.ActionMap` maps event key strings to action key lists
- `control-profiles.json` maps `eventKey -> command` for the active console control profile
- profile data maps `eventKey -> Spotify URI[]`
- `SpotifyProfileAction` is still available for play-only behavior on profile-mapped events
- the legacy `profiles.json` contract remains available for track-routing work
- the default console-first path focuses on explicit playback control scenarios, not a large rule-authoring system

## CS2 Setup

`Cs2SetupService` owns CS2 onboarding.

It can:

- detect the game install root
- use `UNDEFAULTIT_CS2_PATH` as an override
- scan common Steam install locations and `libraryfolders.vdf`
- generate `game/csgo/cfg/gamestate_integration_undefaultit.cfg`
- compare the existing file with the expected generated content
- install automatically during backend startup

The generated GSI URI is config-driven:

- `Gsi.Url` + `Gsi.Path`
- defaults to `http://localhost:5292/gsi` when config is missing

## Host Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/gsi` | Receive GSI payloads |
| GET | `/status` | Current app status |
| GET | `/events` | Recent normalized events |
| GET | `/config` | Read system config |
| PUT | `/config` | Save system config |
| GET | `/control-profiles` | Read console control profiles |
| PUT | `/control-profiles` | Save console control profiles |
| GET | `/profiles` | Read profiles |
| PUT | `/profiles` | Save profiles |
| GET | `/setup/cs2/status` | Read CS2 setup state |
| POST | `/setup/cs2/install` | Install the CS2 cfg |
| GET | `/spotify/authorize` | Start OAuth in real mode |
| GET | `/spotify/callback` | Finish OAuth in real mode |

## Spotify Runtime Modes

The host supports two modes:

- real mode: uses Spotify Web API, OAuth, encrypted local storage for app credentials, and in-memory access-token storage
- mock mode: uses `MockSpotifyClient` and keeps the backend functional without Spotify credentials

In mock mode:

- the host still accepts GSI payloads
- profile and setup endpoints still work
- OAuth endpoints return a clear unavailable message

## Dependency Injection

Key registrations in `GsiHost/Program.cs`:

- snapshot pipeline services
- `IRulesEngine`
- `IEventAction` implementations
- `IControlProfileService`
- `IPlaybackPolicy`
- configuration and profile services
- CS2 setup service
- snapshot module mappers
- Spotify real or mock client

On startup, the host also tries to ensure the CS2 cfg is installed and prints a console checklist that includes Spotify auth state, CS2 setup state, and the active control profile file.

## Logging

Current logging intent:

- keep startup logs short and informative
- log CS2 setup success or failure once
- keep Spotify mock logs prefixed with `[MOCK]`
- avoid raw payload dumps unless debugging

## Testing

Current backend test coverage focuses on:

- event detection
- snapshot diffing
- rules routing
- control-profile routing and playback behavior
- host endpoints
- config-driven CS2 setup
- mock Spotify behavior

## What Is Intentionally Not In Scope

- Dota 2 support
- persistent Spotify OAuth token storage across process restarts
- real-time push updates to the UI
- a large rule authoring system
- multi-game playback orchestration
