# Quick Launch

Start `GsiHost` for local iteration. Use `--quick` to bring the backend up immediately without CS2 auto-setup or real Spotify OAuth.

The normal startup path is Windows-only because it uses the encrypted Windows secret store (DPAPI, CurrentUser scope) for the Spotify `CLIENT_ID`. Real Spotify playback control also requires Spotify Premium and an active playback device.

## Fastest start

```powershell
dotnet run --project .\GsiHost -- --quick
```

`--quick` mode gives you mock Spotify (no OAuth, no credential prompts), CS2 auto-setup skipped, Smart Track warmup skipped, and best-effort optional diagnostics that warn instead of failing startup.

## Real Spotify, faster startup

```powershell
dotnet run --project .\GsiHost -- --skip-cs2-setup
dotnet run --project .\GsiHost -- --skip-smart-track-warmup
```

## Spotify mode overrides

- `--use-mock-spotify` forces mock mode.
- `--use-real-spotify` forces real OAuth and disables `--quick` defaults.

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
