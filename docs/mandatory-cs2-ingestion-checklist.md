# Mandatory CS2 ingestion checklist (before music controller work)

These fields must be **first-class** in the observation / snapshot model (or explicitly marked unsupported with a policy fallback). Otherwise safety and adaptive features become heuristics.

## Checklist

| Input | Required for | Status in codebase |
|-------|----------------|----------------------------------------|
| Tactical pause / game paused | `GameClock.IsGamePaused`, danger windows | **TBD** — `Cs2GameAdapter.GameClockSnapshot.IsGamePaused` is hard-coded to `false` because no reliable signal is mapped yet. Extend GSI DTO + mapper. |
| Post-round timing windows | Hysteresis, danger at end-of-round | **TBD** |
| Spectator vs active player | `SpectatorOrObserver`, safety | Heuristic — `Cs2GameAdapter` derives `NeutralContext.SpectatorOrObserver` from `player.activity != "playing"` or a missing `state` block. Conservative: returns null when the signal is genuinely unknown. A dedicated GSI signal would be more robust. |
| Explicit freeze / live / end boundaries | Phase-neutral `MatchPhaseNeutral` | Done — `Cs2GameAdapter.MapMatchPhase` translates `live / freezetime / warmup / intermission / gameover` to `MatchPhaseNeutral`. Unknown values fall through to `Unknown`. CS2 phase strings stay inside the adapter. |
| Stale observation age | `Unknown` / `Danger` escalation | Done — `Cs2GameAdapter` flags `SafetyFacts.IsStale = true` when `receivedAt - provider.timestamp > 5s` (`Cs2GameAdapter.ProviderTimestampStaleThreshold`). Missing provider timestamp keeps `IsStale = false` and records `no-provider-timestamp` in `Reason`. |
| Bomb planted / timer / defusing state | Defusal tension + danger | **TBD** — bomb / objective DTOs are not parsed in `GsiHost/Dtos`, so `NeutralContext.ObjectivePressure` stays null today (TODO marker in `Cs2GameAdapter`). |

## Acceptance gate

No release of the new music safety pipeline without:

1. Documented mapping from CS2 JSON keys → each row above (or explicit “unsupported + conservative default”).
2. Unit tests for mapper + staleness at least for supported rows.

## Related docs

- [ingestion-spec-cs2-dota.md](ingestion-spec-cs2-dota.md)
- [cs2-gsi-events.md](cs2-gsi-events.md)
