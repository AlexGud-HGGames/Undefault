# UndefaultIt

UndefaultIt is a small desktop app that listens to CS2 GSI events and controls Spotify around gameplay moments.

## Current Mini-Version

The current build is intentionally narrow:

- the backend accepts CS2 Game State Integration payloads at `POST /gsi`
- the detector currently focuses on `round_start` and `death`
- the default console flow routes both events through `spotify.control_profile`
- the default control profile ducks Spotify on `round_start`
- the default control profile restores the last saved volume on `death`
- CS2 setup is handled by the backend and can generate the required GSI cfg automatically
- the normal console path uses real Spotify OAuth with encrypted local storage for app credentials
- mock Spotify still exists for tests and targeted development scenarios

The UI is a separate Avalonia client that talks to the backend over HTTP.

## Prerequisites

- .NET 8 SDK
- CS2 installed locally if you want to use the real GSI flow
- Spotify credentials only if you want real Spotify mode

## Run Flow

1. Start the backend:

```powershell
dotnet run --project .\GsiHost
```

2. Follow the console checklist:

- confirm the redirect URI is `http://127.0.0.1:5292/callback`
- open the printed Spotify authorization URL if Spotify is not authenticated yet
- verify the generated CS2 GSI target URL
- edit `GsiHost/control-profiles.json` if you want to change the default music-control scenario

3. Start the UI in a second terminal only if you want the desktop app:

```powershell
dotnet run --project .\UI
```

4. If you start the UI, let it connect to the backend on `http://localhost:5292`.

## Backend Endpoints

The main endpoints are:

- `POST /gsi`
- `GET /status`
- `GET /events`
- `GET /config`
- `PUT /config`
- `GET /control-profiles`
- `PUT /control-profiles`
- `GET /profiles`
- `PUT /profiles`
- `GET /setup/cs2/status`
- `POST /setup/cs2/install`
- `GET /spotify/authorize`
- `GET /spotify/callback`

## Configuration

Important settings live in `GsiHost/appsettings.json`:

- `Gsi.Url` controls the generated CS2 GSI target URL
- `UseMockSpotify` switches between mock and real Spotify mode for non-console/test scenarios
- `SpotifyVolumeDuck` controls mute and restore volume values
- `RulesEngine.ActionMap` maps event keys such as `round_start` and `death` to actions like `spotify.control_profile`

The default console-first music scenario lives in `GsiHost/control-profiles.json`:

- `duck` lowers Spotify volume to a target percentage
- `restore_volume` restores the previously saved volume after a duck
- `pause` turns music off by pausing playback
- `resume` turns music back on by resuming playback

The legacy track-based profile document remains available through `GET /profiles` and `PUT /profiles`, but the new console-first scenario path is the dedicated control profile file.

## Logs To Expect

In normal startup you should see logs similar to:

- CS2 GSI setup status
- whether Spotify credentials are ready
- the active control profile file and active profile id
- event processing and action execution

In mock mode the Spotify client logs with a `[MOCK]` prefix.

## Intentionally Not Implemented

- Dota 2 support
- persistent Spotify OAuth token storage across process restarts
- real-time push updates to the UI
- a large multi-game rules system

## Documentation

- `BACKEND_ARCHITECTURE.md` for the current backend flow
- `CS2_GSI_EVENTS.md` for the practical CS2 event surface and future profile candidates
- `ROADMAP.md` for forward-looking work
