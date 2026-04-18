# UndefaultIt

UndefaultIt is a console-first local backend: CS2 Game State Integration posts to **GsiHost**, which controls Spotify around gameplay moments. There is no separate desktop UI in this repository—the host HTTP API is enough for automation and manual checks (`curl`, browser, scripts).

**More documentation:** [docs/README.md](docs/README.md) (architecture, CS2 reference, roadmap).

## Agent Context

### Project

- `UndefaultIt` is a local .NET backend for gameplay-driven Spotify control.
- Current runtime focus: `CS2` + `GsiHost`.
- Current default behavior: `round_start` / `death` -> `spotify.control_profile`.

### Modules

- `Core/` — models, diffing, detection, rules, Spotify abstractions, `Core/Music/` contracts.
- `GsiHost/` — HTTP host, GSI mapping, processing pipeline, config, OAuth, CS2 setup.
- `Core.Tests/`, `GsiHost.Tests/` — unit and integration coverage.

### Runtime flow

`CS2 GSI` -> `POST /gsi` -> `GsiProcessingService` -> `GsiSnapshotMapper` -> `GameSnapshot` -> `SnapshotDiffer` -> `EventDetector` -> `RulesEngine` -> `IEventAction`

### Constraints

- No YAML scenario engine.
- No Dota 2 runtime support yet.
- Default Spotify control path: `spotify.control_profile` + `GsiHost/control-profiles.json`.
- Safety-first music architecture is documented; runtime integration is partial.

### Read first

- [docs/README.md](docs/README.md)
- [docs/backend-architecture.md](docs/backend-architecture.md)
- [docs/music-safety-state-spec.md](docs/music-safety-state-spec.md)
- [docs/failure-safety-spec.md](docs/failure-safety-spec.md)
- [docs/volume-composition-spec.md](docs/volume-composition-spec.md)
- [docs/stability-and-device-layer-spec.md](docs/stability-and-device-layer-spec.md)
- [docs/neutral-signals-and-game-clock.md](docs/neutral-signals-and-game-clock.md)
- [docs/ingestion-spec-cs2-dota.md](docs/ingestion-spec-cs2-dota.md)
- [docs/rules-engine-migration.md](docs/rules-engine-migration.md)
- [docs/roadmap.md](docs/roadmap.md)

## How the backend path works

You can picture one straight line:

**CS2 GSI** → **`POST /gsi` on GsiHost** → **`GsiProcessingService`** maps JSON to a snapshot → **`RulesEngine`** diffs the last snapshot, runs **`EventDetector`**, then runs **`IEventAction`** instances from **`RulesEngine.ActionMap`**.

For the default console-first music path, you map normalized events (for example `round_start`, `death`) to **`spotify.control_profile`**. That action reads the active rules in **`GsiHost/control-profiles.json`** and delegates pause, resume, duck, and restore to **`ISpotifyPlaybackControl`** (**`SpotifyPlaybackControlCoordinator`**). There is no separate YAML scenario engine; behavior is **config + code** only.

## Current Mini-Version

The current build is intentionally narrow:

- the backend accepts CS2 Game State Integration payloads at `POST /gsi`
- the detector currently focuses on `round_start` and `death`
- the default console flow routes both events through `spotify.control_profile`
- the default control profile ducks Spotify on `round_start`
- the default control profile restores the last saved volume on `death`
- CS2 setup is handled by the backend and can generate the required GSI cfg automatically
- the normal console path uses real Spotify OAuth with encrypted local storage for app credentials
- mock Spotify still exists for tests and when you deliberately run without real OAuth

## Prerequisites

- .NET 8 SDK for the app projects (`Core`, `GsiHost`)
- .NET 9 SDK if you want to run the full test solution (`Core.Tests` targets `net9.0`)
- CS2 installed locally if you want to use the real GSI flow
- Spotify credentials only if you want real Spotify mode

## Run Flow

1. Start the backend:

```powershell
dotnet run --project .\GsiHost
```

2. Optional quick launch for local testing (mock Spotify):

```powershell
dotnet run --project .\GsiHost -- --quick
```

- Uses the mock Spotify client (no OAuth and no credential prompts)
- Skips CS2 auto-setup and Smart Track warmup by default
- Still starts the host so you can test endpoints immediately
- If optional setup/diagnostics fail, the host continues and logs warnings
- `--use-real-spotify` removes the quick-launch defaults and keeps the normal startup path

3. Follow the console checklist:

- confirm the redirect URI is `http://127.0.0.1:5292/callback`
- open the printed Spotify authorization URL if Spotify is not authenticated yet
- verify the generated CS2 GSI target URL
- edit `GsiHost/control-profiles.json` if you want to change duck, restore, pause, or resume per event (after `RulesEngine.ActionMap` routes that event to `spotify.control_profile`)

4. Optional: call the host over HTTP (for example `Invoke-RestMethod http://127.0.0.1:5292/status`) to confirm it is up.

## Backend Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/` | Short host banner (sanity check) |
| `POST` | `/gsi` | Accept CS2 GSI JSON |
| `GET` | `/status` | Host / runtime status |
| `GET` | `/events` | Recent normalized events |
| `GET` | `/config` | Read editable host config |
| `PUT` | `/config` | Save editable host config |
| `GET` | `/control-profiles` | Read console control profiles |
| `PUT` | `/control-profiles` | Save console control profiles |
| `GET` | `/profiles` | Read legacy track profiles |
| `PUT` | `/profiles` | Save legacy track profiles |
| `GET` | `/setup/cs2/status` | CS2 GSI cfg status |
| `POST` | `/setup/cs2/install` | Install or update CS2 GSI cfg |
| `GET` | `/spotify/authorize` | Authorization URL (real Spotify mode) |
| `GET` | `/callback` | OAuth callback (primary) |
| `GET` | `/spotify/callback` | OAuth callback (alternate path) |
| `GET` | `/spotify/status` | Spotify auth / mode diagnostics |

## Configuration

### `appsettings.json` (host)

| Section / key | What you use it for |
|----------------|---------------------|
| `Gsi` (`Method`, `Path`, `Url`) | Generated CS2 GSI target URL and related wiring |
| `Spotify` | OAuth client id/secret, redirect URI, scopes |
| `UseMockSpotify` | Mock vs real Spotify client (console bootstrap forces real for the normal console run) |
| `EventDetector` | Which normalized events fire (`EnableRoundStart`, `EnableDeath`, `EnableCombat`, `EnableIdle`, `RoundStartPhase`, `DeathCooldown`; optional combat/idle tuning keys match `EventDetectorOptions` in Core) |
| `SpotifyVolumeDuck` | Defaults for **`SpotifyPlaybackControlCoordinator`**: `MuteVolume` is the duck target when a control rule omits a volume; `FallbackRestoreVolume` is used when there is no saved pre-duck volume to restore |
| `SmartTrackStart` | Optional non-zero start positions for **track** playback (`spotify.profile`), not for control-profile commands |
| `RulesEngine.ActionMap` | **Source of truth for which actions run** per event key (e.g. `round_start` → `spotify.control_profile`) |

Registered action keys you can list in `ActionMap` today include `log`, `spotify.control_profile`, `spotify.profile`, and `spotify.volume_duck`. The default console path uses only `spotify.control_profile` for `round_start` and `death`.

### `control-profiles.json` (console-first music)

**Source of truth for what happens to Spotify** after an event reaches `spotify.control_profile`:

- `duck` lowers Spotify volume to a target percentage
- `restore_volume` restores the previously saved volume after a duck
- `pause` turns music off by pausing playback
- `resume` turns music back on by resuming playback

If you map `spotify.volume_duck` in `ActionMap` instead, you use the older dedicated duck/restore action; the default path prefers **`spotify.control_profile`** plus **`control-profiles.json`** so pause, resume, duck, and restore all go through one coordinator.

The legacy track-based profile document remains available through `GET /profiles` and `PUT /profiles` (`profiles.json` in the host content root). Use it for **`spotify.profile`** (URI lists per event), not for the default duck/restore console flow.

Smart Track Start is a separate optional module for track-starting flows such as `spotify.profile`:

- enable it in `SmartTrackStart.Enabled`
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

## Documentation

Detailed docs are in **[`docs/`](docs/README.md)**:

- [`docs/README.md`](docs/README.md) — documentation index
- [`docs/backend-architecture.md`](docs/backend-architecture.md) — full current backend pipeline (`RulesEngine`, actions, files, endpoints)
- [`docs/cs2-gsi-events.md`](docs/cs2-gsi-events.md) — CS2 signal surface and future profile ideas
- [`docs/music-safety-state-spec.md`](docs/music-safety-state-spec.md) — authoritative `Unknown` / `Safe` / `Danger` model
- [`docs/failure-safety-spec.md`](docs/failure-safety-spec.md) — stale input, Spotify failures, degraded device rules
- [`docs/volume-composition-spec.md`](docs/volume-composition-spec.md) — canonical gain / transport merge rules
- [`docs/stability-and-device-layer-spec.md`](docs/stability-and-device-layer-spec.md) — evaluation tick, coalescing, emergency suppression lane
- [`docs/neutral-signals-and-game-clock.md`](docs/neutral-signals-and-game-clock.md) — neutral cross-game signals and clock contract
- [`docs/mandatory-cs2-ingestion-checklist.md`](docs/mandatory-cs2-ingestion-checklist.md) — required CS2 inputs before controller work
- [`docs/ingestion-spec-cs2-dota.md`](docs/ingestion-spec-cs2-dota.md) — planned CS2 ingestion extensions and future Dota shape
- [`docs/mixer-contract-and-device-wiring.md`](docs/mixer-contract-and-device-wiring.md) — `IMusicMixer` boundary and Spotify wiring direction
- [`docs/music-engine-config-schema-v1.md`](docs/music-engine-config-schema-v1.md) — config and observability DTO schema
- [`docs/rules-engine-migration.md`](docs/rules-engine-migration.md) — migration rule: one orchestration entry, no double side effects
- [`docs/mvp-priorities-and-confirmation.md`](docs/mvp-priorities-and-confirmation.md) — current safety-first MVP defaults
- [`docs/roadmap.md`](docs/roadmap.md) — forward-looking work
