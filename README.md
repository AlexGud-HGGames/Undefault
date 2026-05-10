# UndefaultIt

[![CI](https://github.com/AlexGud-HGGames/Undefault/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/AlexGud-HGGames/Undefault/actions/workflows/ci.yml)

UndefaultIt is a Windows-first, console-first local backend for gameplay-driven Spotify control. CS2 Game State Integration posts to `**GsiHost**`, the host turns payload changes into normalized events, and configured actions control Spotify around gameplay moments.

There is no separate desktop UI in this repository. The console checklist, JSON config files, and local HTTP API are the product surface for now.

**More documentation:** [docs/README.md](docs/README.md) (architecture, CS2 reference, quick launch, roadmap).

## What Works Today

- CS2 posts Game State Integration payloads to `POST /gsi`.
- `round_start` and `death` are enabled by default.
- Both events route through `spotify.control_profile`.
- The default control profile ducks Spotify to `0%` on `round_start`.
- The default control profile restores the last saved volume on `death`.
- The backend can generate the required CS2 GSI cfg automatically.
- Real Spotify mode uses OAuth and encrypted local storage for Spotify app credentials.
- Mock Spotify mode is available for fast local checks and tests.
- A **manual intent timeline** records normalized GSI events and optional `POST /user-actions` entries in one ordered log, with game context on manual rows. This is **tester / product-owner tooling only**: the timeline endpoints, `/user-actions`, and Windows hotkeys are mapped only when the host runs in **intent-capture runtime mode** (see [Runtime modes](#runtime-modes) and [docs/manual-intent-timeline.md](docs/manual-intent-timeline.md)).

## Important Limits

- Runtime startup is Windows-only today because console bootstrap uses the encrypted Windows secret store.
- Real Spotify playback control requires Spotify Premium and an active playback device.
- OAuth access and refresh tokens are kept in memory, so Spotify auth must be repeated after process restart.
- Spotify integration is limited to local playback control; do not frame the project as synchronized Spotify music or a game soundtrack.
- Smart Track Start only changes `position_ms` for track playback through `spotify.profile`; it does not affect duck, pause, resume, or restore commands.
- Safety-first music architecture is documented, but runtime integration is still partial.

## Agent Context

### Project

- `UndefaultIt` is a Windows-first local .NET backend for gameplay-driven Spotify control.
- Current runtime focus: `CS2` + `GsiHost`.
- Current default behavior: `round_start` / `death` -> `spotify.control_profile`.

### Modules

- `Core/` — models, diffing, detection, rules, Spotify abstractions, `Core/Music/` contracts.
- `GsiHost/` — HTTP host, GSI mapping, processing pipeline, config, OAuth, CS2 setup.
- `Cs2Simulator/`, `Cs2Simulator.Runtime/`, `Cs2Simulator.Scenarios/` — local CS2 GSI simulator (console + transport/runner library + scenarios); see [docs/cs2-simulator.md](docs/cs2-simulator.md).
- `Core.Tests/`, `GsiHost.Tests/`, `Cs2Simulator.Tests/` — unit and integration coverage.

### Runtime flow

`CS2 GSI` -> `POST /gsi` -> `GsiProcessingService` -> `GsiSnapshotMapper` -> `GameSnapshot` -> `SnapshotDiffer` -> `EventDetector` -> `RulesEngine` -> `IEventAction`

### Constraints

- No YAML scenario engine.
- No Dota 2 runtime support yet.
- Default Spotify control path: `spotify.control_profile` + `GsiHost/control-profiles.json`.
- Real Spotify control requires Premium and an active playback device.
- Spotify features must stay inside the [local playback control boundary](docs/spotify-playback-policy-boundary.md).
- Safety-first music architecture is documented; runtime integration is partial.

### Read first

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

## How the backend path works

You can picture one straight line:

**CS2 GSI** → `**POST /gsi` on GsiHost** → `**GsiProcessingService`** maps JSON to a snapshot → `**SnapshotDiffer`** compares it with the previous snapshot → `**EventDetector`** emits normalized events → `**RulesEngine.ActionMap**` chooses `**IEventAction**` instances.

For the default console-first music path, you map normalized events (for example `round_start`, `death`) to `**spotify.control_profile**`. That action reads the active rules in `**GsiHost/control-profiles.json**` and delegates pause, resume, duck, and restore to `**ISpotifyPlaybackControl**` (`**SpotifyPlaybackControlCoordinator**`). There is no separate YAML scenario engine; behavior is **config + code** only.

## Prerequisites

- Windows for the normal host startup path
- .NET 8 SDK for the full solution (all projects, including tests, target `net8.0`)
- CS2 installed locally if you want to use the real GSI flow
- Spotify app credentials only if you want real Spotify mode
- Spotify Premium and an active playback device for real pause, resume, play, and volume commands

## Run Flow

Fast local check with mock Spotify:

```powershell
dotnet run --project .\GsiHost -- --quick
```

Normal console flow with real Spotify and CS2 setup:

```powershell
dotnet run --project .\GsiHost
```

Then follow the printed console checklist:

- register or confirm the Spotify redirect URI: `http://127.0.0.1:5292/callback`
- open the printed Spotify authorization URL if Spotify is not authenticated yet
- verify the generated CS2 GSI target URL
- edit `GsiHost/control-profiles.json` to change duck, restore, pause, or resume behavior
- call `Invoke-RestMethod http://127.0.0.1:5292/status` to confirm the host is up

The default HTTP URL is `http://127.0.0.1:5292`. `launchSettings.json` also contains an HTTPS profile (`https://127.0.0.1:7295`) for development, but the documented Spotify callback uses the HTTP loopback URL above.

### Runtime modes

The host has two explicit runtime modes, selected by `Runtime:Mode` in `appsettings.json` or by a CLI flag. CLI flags win over config; the default is `scenario_playback`.

#### Scenario playback mode (default end-user mode)

GSI-driven rules in `RulesEngine.ActionMap` control Spotify through `spotify.control_profile`. Tester tooling is **not mapped**: `/timeline`, `/timeline/episodes`, `/user-actions`, and Windows hotkeys return `404 Not Found`. `WindowsHotkeyService` is not registered. This is the normal user run.

```powershell
dotnet run --project .\GsiHost
```

#### Intent capture mode (tester / product-owner tooling)

GSI is still ingested for context and recent-event history, but `RulesEngine.DetectAsync` runs **without executing actions**, so captured intent data is not polluted by automatic Spotify side effects. `/timeline`, `/timeline/episodes`, and `/user-actions` are mapped, and `WindowsHotkeyService` is registered. Each tester subsystem still requires its own `Enabled` flag (`Timeline`, `ManualMusicActions`, `Keybinds`).

```powershell
dotnet run --project .\GsiHost -- --intent-capture
```

Equivalent JSON config (turn on the per-feature flags as needed):

```json
{
  "Runtime": { "Mode": "intent_capture" },
  "Timeline": { "Enabled": true },
  "ManualMusicActions": { "Enabled": true },
  "Keybinds": { "Enabled": true }
}
```

Accepted `Runtime:Mode` values are `scenario_playback` (default) and `intent_capture`. Unknown / empty values fall back to `scenario_playback`. See [docs/manual-intent-timeline.md](docs/manual-intent-timeline.md) for the full tester-facing surface.

### Useful Startup Flags


| Flag                        | Use it when                                                                      |
| --------------------------- | -------------------------------------------------------------------------------- |
| `--quick`                   | You want mock Spotify, no OAuth prompts, no CS2 setup, and no Smart Track warmup |
| `--use-real-spotify`        | You used `--quick` defaults before but now want the normal real Spotify path     |
| `--use-mock-spotify`        | You want mock Spotify explicitly                                                 |
| `--skip-cs2-setup`          | You want real Spotify but do not want automatic CS2 cfg install/update           |
| `--skip-smart-track-warmup` | You want faster startup without Smart Track Start preload                        |
| `--reset-spotify-secrets`   | You want to overwrite saved Spotify app credentials                              |
| `--clear-spotify-secrets`   | You want to remove saved Spotify app credentials                                 |
| `--intent-capture`          | You are a tester / product-owner and want `/timeline`, `/user-actions`, and Windows hotkeys mapped (sets `Runtime:Mode = intent_capture`) |
| `--scenario-playback`       | You want to force the default end-user mode even if `appsettings.json` sets `Runtime:Mode = intent_capture` |


Spotify credentials can come from:

```powershell
$env:CLIENT_ID = "..."
$env:CLIENT_SECRET = "..."
```

If env vars are not set, the console bootstrap can use the encrypted local store, `appsettings.json`, or an interactive prompt.

### Local CS2 simulator

For development and testing without launching CS2, run the bundled simulator
in a second terminal: `dotnet run --project .\Cs2Simulator`. It posts
realistic CS2-shaped payloads (`provider`, `map`, `round`, `player`) to
`POST /gsi` over scenarios like `t-side-round`, `ct-defense`, `ct-defense-fail`,
`clutch-1v3`, `death-spectator`, and `tactical-pause`. Configurable speed, step mode,
and a `--scenario X --once` flag for scripted runs. See
[docs/cs2-simulator.md](docs/cs2-simulator.md).

## Backend Endpoints


| Method | Path                 | Purpose                                                                                                      |
| ------ | -------------------- | ------------------------------------------------------------------------------------------------------------ |
| `GET`  | `/`                  | Short host banner (sanity check)                                                                             |
| `POST` | `/gsi`               | Accept CS2 GSI JSON                                                                                          |
| `POST` | `/gsi/reset`         | Reset detector state, snapshot store, recent events ring, and timeline session (used by the local simulator) |
| `GET`  | `/status`            | Host / runtime status                                                                                        |
| `GET`  | `/events`            | Recent normalized events                                                                                     |
| `GET`  | `/timeline`          | Recent unified timeline (GSI + manual actions) — **intent_capture only**, returns `404` in `scenario_playback` |
| `GET`  | `/timeline/episodes` | Manual-intent episodes with before/after entry windows — **intent_capture only**, returns `404` in `scenario_playback` |
| `POST` | `/user-actions`      | Record manual music intent; apply matching `control-profiles.json` rule — **intent_capture only**, returns `404` in `scenario_playback` |
| `GET`  | `/config`            | Read editable host config                                                                                    |
| `PUT`  | `/config`            | Save editable host config                                                                                    |
| `GET`  | `/control-profiles`  | Read console control profiles                                                                                |
| `PUT`  | `/control-profiles`  | Save console control profiles                                                                                |
| `GET`  | `/profiles`          | Read legacy track profiles                                                                                   |
| `PUT`  | `/profiles`          | Save legacy track profiles                                                                                   |
| `GET`  | `/setup/cs2/status`  | CS2 GSI cfg status                                                                                           |
| `POST` | `/setup/cs2/install` | Install or update CS2 GSI cfg                                                                                |
| `GET`  | `/spotify/authorize` | Authorization URL (real Spotify mode)                                                                        |
| `GET`  | `/callback`          | OAuth callback (primary)                                                                                     |
| `GET`  | `/spotify/callback`  | OAuth callback (alternate path)                                                                              |
| `GET`  | `/spotify/status`    | Spotify auth / mode diagnostics                                                                              |


## Configuration

### `appsettings.json` (host)


| Section / key                                     | What you use it for                                                                                                                                                                                         |
| ------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Gsi` (`Method`, `Path`, `Url`, `AllowReset`)     | Generated CS2 GSI target URL and related wiring; `AllowReset` gates `POST /gsi/reset` (default true)                                                                                                        |
| `Spotify`                                         | OAuth client id/secret, redirect URI, scopes (`CLIENT_ID` / `CLIENT_SECRET` env vars override appsettings)                                                                                                  |
| `UseMockSpotify`                                  | Mock vs real Spotify client (console bootstrap forces real for the normal console run)                                                                                                                      |
| `EventDetector`                                   | Which normalized events fire (`EnableRoundStart`, `EnableDeath`, `EnableCombat`, `EnableIdle`, `RoundStartPhase`, `DeathCooldown`; optional combat/idle tuning keys match `EventDetectorOptions` in Core)   |
| `SpotifyVolumeDuck`                               | Defaults for `**SpotifyPlaybackControlCoordinator**`: `MuteVolume` is the duck target when a control rule omits a volume; `FallbackRestoreVolume` is used when there is no saved pre-duck volume to restore |
| `SmartTrackStart` (`Enabled`, `PreloadOnStartup`) | Optional non-zero start positions for **track** playback (`spotify.profile`), not for control-profile commands                                                                                              |
| `Runtime` (`Mode`)                                | Selects runtime mode: `scenario_playback` (default) or `intent_capture`. CLI flags `--intent-capture` / `--scenario-playback` override this. Tester endpoints and Windows hotkeys only register in `intent_capture` (`RuntimeOptions`)                          |
| `Timeline`                                        | Manual intent timeline: enable flag, ring size, JSONL directory, episode window sizes (`TimelineOptions`). **Intent-capture only** — has no effect when `Runtime:Mode = scenario_playback`                                                                       |
| `ManualMusicActions`                              | Gate and optional allowlist for `POST /user-actions` (`ManualMusicActionOptions`). **Intent-capture only** — `/user-actions` is not mapped in `scenario_playback`                                                                                               |
| `Keybinds`                                        | Optional Windows global hotkeys that invoke the same path as `POST /user-actions` (`KeybindOptions`; off by default). **Intent-capture only** — `WindowsHotkeyService` is not registered in `scenario_playback`                                                  |
| `RulesEngine.ActionMap`                           | **Source of truth for which actions run** per **detector** event key from GSI (e.g. `round_start` → `spotify.control_profile`). Manual actions do **not** use this map.                                     |


Registered action keys you can list in `ActionMap` today include `log`, `spotify.control_profile`, `spotify.profile`, and `spotify.volume_duck`. The default console path uses only `spotify.control_profile` for `round_start` and `death`.

### `control-profiles.json` (console-first music)

**Source of truth for what happens to Spotify** after an event reaches `spotify.control_profile`:

- `duck` lowers Spotify volume to a target percentage
- `restore_volume` restores the previously saved volume after a duck
- `pause` turns music off by pausing playback
- `resume` turns music back on by resuming playback

If you map `spotify.volume_duck` in `ActionMap` instead, you use the older dedicated duck/restore action; the default path prefers `**spotify.control_profile`** plus `**control-profiles.json`** so pause, resume, duck, and restore all go through one coordinator.

**Manual music control:** `POST /user-actions` uses the same `control-profiles.json` rules keyed by `eventKey` (often `custom:…`), but it does **not** go through `RulesEngine.ActionMap`. Add rules for those keys if you want manual intents to change playback.

The legacy track-based profile document remains available through `GET /profiles` and `PUT /profiles` (`profiles.json` in the host content root). Use it for `**spotify.profile`** (URI lists per event), not for the default duck/restore console flow.

Smart Track Start is a separate optional module for track-starting flows such as `spotify.profile`:

- enable it in `SmartTrackStart.Enabled`
- keep or disable startup preload with `SmartTrackStart.PreloadOnStartup`
- add manual per-track start offsets in `GsiHost/smart-track-starts.json`
- when enabled, the backend still picks the same URI as before and only changes the `position_ms` used when playback starts
- when disabled or when a track has no entry, playback starts exactly as it does today

## Logs To Expect

In normal startup you should see logs similar to:

- CS2 GSI setup status
- whether Spotify credentials are ready
- the active control profile file and active profile id
- whether Smart Track Start is enabled and which metadata file is being used
- event processing and action execution

In mock mode the Spotify client logs with a `[MOCK]` prefix.

## Intentionally Not Implemented

- Dota 2 support
- persistent Spotify OAuth token storage across process restarts
- real-time push updates to a future client (no live dashboard in-repo)
- a large multi-game rules system
- automatic Smart Track Start analysis from Spotify audio features or external metadata

## Documentation

Detailed docs are in `**[docs/](docs/README.md)`**:

- `[docs/README.md](docs/README.md)` — documentation index
- `[docs/backend-architecture.md](docs/backend-architecture.md)` — full current backend pipeline (`RulesEngine`, actions, files, endpoints)
- `[docs/quick-launch.md](docs/quick-launch.md)` — fast startup flags and failure handling
- `[docs/spotify-playback-policy-boundary.md](docs/spotify-playback-policy-boundary.md)` — product guardrails for Spotify local playback control
- `[docs/cs2-gsi-events.md](docs/cs2-gsi-events.md)` — CS2 signal surface and future profile ideas
- `[docs/cs2-simulator.md](docs/cs2-simulator.md)` — local CS2 GSI simulator (scenarios, CLI, event-mapping)
- `[docs/music-safety-state-spec.md](docs/music-safety-state-spec.md)` — authoritative `Unknown` / `Safe` / `Danger` model
- `[docs/failure-safety-spec.md](docs/failure-safety-spec.md)` — stale input, Spotify failures, degraded device rules
- `[docs/volume-composition-spec.md](docs/volume-composition-spec.md)` — canonical gain / transport merge rules
- `[docs/stability-and-device-layer-spec.md](docs/stability-and-device-layer-spec.md)` — evaluation tick, coalescing, emergency suppression lane
- `[docs/neutral-signals-and-game-clock.md](docs/neutral-signals-and-game-clock.md)` — neutral cross-game signals and clock contract
- `[docs/mandatory-cs2-ingestion-checklist.md](docs/mandatory-cs2-ingestion-checklist.md)` — required CS2 inputs before controller work
- `[docs/ingestion-spec-cs2-dota.md](docs/ingestion-spec-cs2-dota.md)` — planned CS2 ingestion extensions and future Dota shape
- `[docs/mixer-contract-and-device-wiring.md](docs/mixer-contract-and-device-wiring.md)` — `IMusicMixer` boundary and Spotify wiring direction
- `[docs/music-engine-config-schema-v1.md](docs/music-engine-config-schema-v1.md)` — config and observability DTO schema
- `[docs/rules-engine-migration.md](docs/rules-engine-migration.md)` — migration rule: one orchestration entry, no double side effects
- `[docs/manual-intent-timeline.md](docs/manual-intent-timeline.md)` — unified GSI + manual music-action timeline, API, and config
- `[docs/mvp-priorities-and-confirmation.md](docs/mvp-priorities-and-confirmation.md)` — current safety-first MVP defaults
- `[docs/roadmap.md](docs/roadmap.md)` — forward-looking work

