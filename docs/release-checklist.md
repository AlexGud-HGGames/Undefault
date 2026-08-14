# Release / smoke checklist

The product MVP after [2026-08-14](product-pivot-2026-08-14.md) is Tauon automation, not Spotify observe+record.

**This checklist is `PIVOT-9` (manual live Tauon smoke).** Automated coverage for `PIVOT-1`–`PIVOT-8` is in-repo (`dotnet test`). The old UND-64 checklist is in [archive/release-checklist-intent-capture-mvp.md](archive/release-checklist-intent-capture-mvp.md).

## Target prerequisites

- Windows 10/11 x64, .NET 8 SDK.
- Tauon installed, **Enable remote control** on, restarted.
- `GET http://127.0.0.1:7814/api1/status` returns JSON.
- CS2 or `Cs2Simulator`.

## Target smoke (after PIVOT-1–8)

### Tauon running

- [ ] Host starts with `Music:Provider=Tauon` (default).
- [ ] `GET http://127.0.0.1:5292/status` → 200.
- [ ] Simulator (or CS2) emits `round_start` → Tauon resumes (`/api1/play` if paused/stopped).
- [ ] `death` → Tauon pauses (`/api1/pause`).
- [ ] Repeat `death` while already paused → no extra pause storm (idempotent).

### Tauon not running

- [ ] Host starts.
- [ ] GSI / simulator still processed.
- [ ] Music actions log failure; process does not crash.

### Mock

- [ ] `--quick` or `Music:Provider=Mock` runs the same event→action flow without Tauon.

## Until the pivot lands

Use `--quick` + `Cs2Simulator` to exercise GSI/rules against mock Spotify. Do not treat `--mvp` (intent_capture observe) as the product demo.
