# Mandatory CS2 ingestion checklist (before music controller work)

These fields must be **first-class** in the observation / snapshot model (or explicitly marked unsupported with a policy fallback). Otherwise safety and adaptive features become heuristics.

## Checklist

| Input | Required for | Status in codebase (when doc written) |
|-------|----------------|----------------------------------------|
| Tactical pause / game paused | `GameClock.IsGamePaused`, danger windows | **TBD** — extend GSI DTO + mapper |
| Post-round timing windows | Hysteresis, danger at end-of-round | **TBD** |
| Spectator vs active player | `SpectatorOrObserver`, safety | **TBD** — may need GSI `player_activity` / `provider` fields |
| Explicit freeze / live / end boundaries | Phase-neutral `MatchPhaseNeutral` | Partial — `RoundModule.Phase` exists; may need richer mapping |
| Stale observation age | `Unknown` / `Danger` escalation | **TBD** — timestamp exists on `GameSnapshot` |
| Bomb planted / timer / defusing state | Defusal tension + danger | **TBD** — not in current diff modules |

## Acceptance gate

No release of the new music safety pipeline without:

1. Documented mapping from CS2 JSON keys → each row above (or explicit “unsupported + conservative default”).
2. Unit tests for mapper + staleness at least for supported rows.

## Related docs

- [ingestion-spec-cs2-dota.md](ingestion-spec-cs2-dota.md)
- [cs2-gsi-events.md](cs2-gsi-events.md)
