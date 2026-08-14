# Quick Launch

Start `GsiHost` for local iteration.

> **Leftover Spotify flags.** `--use-real-spotify`, `--reset-spotify-secrets`, `--clear-spotify-secrets`, and `/spotify/*` are leftover until `PIVOT-11`. They are not the product launch path.
>
> **Current binary:** `--quick` sets `Music:Provider=Mock` (no Tauon, no CS2 auto-setup). Default non-quick provider is Tauon (`http://127.0.0.1:7814`). Leftover Spotify stays mock unless `--use-real-spotify`. See [roadmap.md](roadmap.md) and [tauon-integration.md](tauon-integration.md).

## Fastest start

```powershell
dotnet run --project .\GsiHost -- --quick
```

`--quick` mode uses `MockMusicPlayer`, skips CS2 auto-setup and Smart Track warmup, and keeps leftover Spotify on the mock client.

## Real Spotify, faster startup

```powershell
dotnet run --project .\GsiHost -- --skip-cs2-setup
dotnet run --project .\GsiHost -- --skip-smart-track-warmup
```

## Spotify mode overrides

- `--use-mock-spotify` forces mock mode.
- `--use-real-spotify` forces real OAuth and disables `--quick` defaults.

## Runtime / MVP flags

| Flag | Use when |
| --- | --- |
| `--quick` | Mock Spotify, skip CS2 setup and Smart Track warmup |
| `--mvp` | One-command MVP: `intent_capture` + Timeline + PlaybackObserver ON in memory (does not mutate `appsettings.json`) |
| `--intent-capture` | Map `/timeline` and register `PlaybackStateObserver` |
| `--scenario-playback` | Force default end-user mode (GSI rules drive Spotify) |
| `--skip-cs2-setup` | Real Spotify without automatic CS2 cfg install |
| `--skip-smart-track-warmup` | Faster startup without Smart Track preload |
| `--reset-spotify-secrets` | Overwrite saved Spotify `CLIENT_ID` |
| `--clear-spotify-secrets` | Wipe the encrypted credential store |

`--mvp` is the **legacy** UND-64 observe+record mode, not the Tauon product MVP. Timeline notes: [manual-intent-timeline.md](manual-intent-timeline.md). HTTP table: [backend-architecture.md](backend-architecture.md).

## Spotify credentials (PKCE, post-UND-47)

Spotify OAuth uses Authorization Code with PKCE, so the desktop client carries no `client_secret`. Only the public `CLIENT_ID` is needed.

Sources, in resolution order (first non-empty wins):

1. `CLIENT_ID` environment variable.
2. Encrypted local store (Windows DPAPI; path printed in the startup checklist).
3. `Spotify:ClientId` in `appsettings.json`.
4. Interactive console prompt (only if 1–3 are empty).

Notes:

- `CLIENT_SECRET` is no longer read. If it is set in the environment, the host emits one DEBUG line saying it is being ignored; the value itself is never read or echoed.
- `--reset-spotify-secrets` overwrites the cached `CLIENT_ID`.
- `--clear-spotify-secrets` wipes the encrypted store. With PKCE there is no `client_secret` to clear; the flag still removes the cached `CLIENT_ID` (and any legacy `client_secret` blob from a pre-UND-47 install).

## Failure handling

CS2 auto-setup and Smart Track warmup are best-effort. If reading CS2 setup status or control profiles fails during the startup checklist, the host keeps running and logs a warning instead of terminating.
