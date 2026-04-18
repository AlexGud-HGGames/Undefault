# Ingestion spec — CS2 (v1) and Dota 2 (future)

## CS2 — snapshot extensions (target)

Add modules or structured fields on `GameSnapshot` / DTO layer (host mapping):

| Domain | Suggested module / fields | Source (GSI) |
|--------|----------------------------|--------------|
| Bomb | `BombModule`: planted, site, timer_sec, defusing | `map.round`, `player` bomb-related props — **exact JSON paths TBD per GSI cfg** |
| Pause | `GameClockSnapshot.IsGamePaused` | `map.paused` / `player.state` as available |
| Phase | `MatchPhaseNeutral` derivation | `round.phase`, `round.bomb`, scoreboard |
| Spectator | `SpectatorOrObserver` | `player.activity` / `provider` |
| Staleness | `ReceivedAtUtc` on observation wrapper | Host sets when POST received |

All mappings must be documented in this file as they are implemented (append subsections).

## Dota 2 — future shape

- Separate **mapper** + **TitlePlugin** producing same `GameClockSnapshot` and neutral signals.
- No shared FSM named after CS rounds; engagement/objective pressure only.

## Versioning

Ingestion schema changes bump **`MusicEngineOptions.SchemaVersion`** or a dedicated `IngestionSchemaVersion` when options depend on new fields.
