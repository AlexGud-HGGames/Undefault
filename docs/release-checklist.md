# MVP release checklist + smoke test (intent_capture)

First MVP slice (Linear umbrella UND-64). Target recipient: developer tester running from source. Launch the MVP with one command:

```powershell
dotnet run --project .\GsiHost -- --mvp
```

`--mvp` sets `Runtime:Mode = intent_capture` and turns `Timeline`, `ManualMusicActions`, and `Keybinds` ON in memory. The git-tracked `appsettings.json` default stays `scenario_playback` with the feature flags off.

## Prerequisites

- Windows 10/11 x64, .NET 8 SDK.
- Spotify Premium + an active playback device (desktop or web).
- CS2 installed locally (or use `Cs2Simulator`) when verifying MVP #2.
- A Spotify app `CLIENT_ID` (env `CLIENT_ID`, encrypted local store, `appsettings.json`, or interactive prompt). No `CLIENT_SECRET` (PKCE). For a developer tester, your own dev app is sufficient; `undefault-test` registration (UND-48) is not required for this drop.

## Smoke checklist

### Startup

- [ ] `dotnet run --project .\GsiHost -- --mvp` starts without error.
- [ ] Console checklist shows: loopback URL `http://127.0.0.1:5292`, redirect `http://127.0.0.1:5292/callback`, Spotify CLIENT_ID ready, CS2 cfg readiness + GSI target URL, `MVP launch (--mvp)` line.
- [ ] `GET http://127.0.0.1:5292/status` -> 200.

### MVP #1 - Spotify connect

- [ ] Open the printed Spotify authorization URL (or `GET /spotify/authorize`).
- [ ] Complete browser OAuth; callback hits `/callback`.
- [ ] `GET /spotify/status` -> authenticated, real mode, `127.0.0.1` redirect.

### MVP #2 - CS connect

- [ ] `GET /setup/cs2/status` -> cfg present (or `POST /setup/cs2/install` succeeds).
- [ ] Launch CS2 (or `dotnet run --project .\Cs2Simulator -- --scenario t-side-round --speed max`).
- [ ] On first GSI post, the boxed `CS2 GSI connected` console banner appears.
- [ ] `GET /events` contains `round_start` (or GSI timeline entries appear).

### MVP #3 - Hotkeys -> playback

- [ ] Press `Ctrl+Alt+P` -> Spotify pauses.
- [ ] Press `Ctrl+Alt+R` -> Spotify resumes.
- [ ] Press `Ctrl+Alt+M` -> Spotify ducks (volume to mute target).

### MVP #4 + #5 - Record + persist pause/resume

- [ ] After a real pause, `GET /timeline` shows a `playback` entry with `eventKey: playback_paused` and `timestampUtc`.
- [ ] After a real resume, a `playback` / `playback_resumed` entry appears.
- [ ] A no-op (press pause when already paused) does **not** add a `playback` entry.
- [ ] A JSONL file exists under `{contentRoot}/timeline/session-*.jsonl` with matching lines.
- [ ] `POST /gsi/reset` starts a new session file; subsequent entries append to the new file.

### Security

- [ ] Grep shipped config/logs: no `client_secret`, no `access_token` / `refresh_token` values, no `Bearer` tokens.

## Known limitations (release notes)

- Per-session OAuth: the user must re-authorize after every host restart (in-memory tokens). Cross-restart persistence is Post-MVP.
- MVP runs in `intent_capture` mode; GSI-driven auto-scenarios (`round_start -> duck`, `death -> restore`) are OFF by default in this mode (Post-MVP).
- Windows-only (encrypted `CLIENT_ID` store + global hotkeys).
- No packaged build; run from source. Packaging (UND-32) + `undefault-test` Spotify app (UND-48) are Post-MVP for non-developer testers.
- `playback` timeline entries recorded before the first GSI post carry empty game context.

## Bug report contents

Testers should attach to bug reports:

- host build / version (git commit SHA or branch)
- Windows OS version
- exact steps to reproduce
- relevant console output / logs (confirm no tokens or secrets before sharing)
- Spotify auth state at the time (`GET /spotify/status`)
- CS2 / GSI scenario used (real CS2 or `Cs2Simulator --scenario ...`)
- the `session-*.jsonl` file from `Timeline.Directory` (default `timeline/`), if the issue is about recording

## Rollback

Stop the host. The MVP makes no persistent changes beyond JSONL session files under the configured `Timeline.Directory` (default `timeline/`); delete that folder to clear captured sessions. Git-tracked `appsettings.json` is unchanged by `--mvp` (overrides are in-memory only).
