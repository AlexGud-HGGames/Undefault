# Quick Launch

This guide helps you start `GsiHost` quickly for local iteration. Use `--quick` when you want the backend and endpoints up immediately without CS2 auto-setup or real Spotify OAuth.

The normal host startup path is Windows-only today because it uses the encrypted Windows secret store for Spotify app credentials. Real Spotify playback control also requires Spotify Premium and an active playback device.

## Fastest Start

From the repository root:

```powershell
dotnet run --project .\GsiHost -- --quick
```

In `--quick` mode, you get:

- mock Spotify playback control (no OAuth and no credential prompts)
- CS2 auto-setup skipped by default
- Smart Track warmup skipped by default
- startup continues even if optional diagnostics fail (warnings are logged to the console)

## Real Spotify, Faster Startup

Use these when you want real Spotify, but still want faster startup:

```powershell
dotnet run --project .\GsiHost -- --skip-cs2-setup
```

```powershell
dotnet run --project .\GsiHost -- --skip-smart-track-warmup
```

## Spotify mode overrides

You can explicitly control the Spotify client mode:

- `--use-mock-spotify` forces mock mode
- `--use-real-spotify` forces real OAuth mode and disables the quick-launch defaults

Spotify app credentials can come from `CLIENT_ID` / `CLIENT_SECRET`, the encrypted local store, `appsettings.json`, or the interactive console prompt. Use `--reset-spotify-secrets` to overwrite saved credentials and `--clear-spotify-secrets` to remove them.

## Failure handling behavior

The host treats some startup steps as non-critical:

- CS2 auto-setup and Smart Track warmup are skipped via flags or are best-effort when attempted.
- The startup checklist is best-effort: if reading CS2 setup status or control profiles fails, the host keeps running and logs a warning instead of terminating.

