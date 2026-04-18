# Quick Launch Enablement

This guide helps you start the host quickly for local iteration and testing, without waiting for CS2 auto-setup or real Spotify OAuth.

## Quick start (mock Spotify + skip optional startup)

From the repository root:

```powershell
dotnet run --project .\GsiHost -- --quick
```

In `--quick` mode, you get:

- mock Spotify playback control (no OAuth and no credential prompts)
- CS2 auto-setup skipped by default
- Smart Track warmup skipped by default
- startup continues even if optional diagnostics fail (warnings are logged to the console)

## Targeted skip flags (keep real Spotify)

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

## Failure handling behavior

The host treats some startup steps as non-critical:

- CS2 auto-setup and Smart Track warmup are skipped via flags or are best-effort when attempted.
- The startup checklist is best-effort: if reading CS2 setup status or control profiles fails, the host keeps running and logs a warning instead of terminating.

