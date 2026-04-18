# Neutral signals and GameClock

Shared Core and host logic must **not** embed CS2-only mode names as cross-title types. Title plugins map into **neutral** facts and signals.

## GameClock (authoritative time contract)

| Field | Purpose |
|-------|---------|
| `WallTimeUtc` | Host clock for staleness and HTTP. |
| `GameTimeSeconds` | If available from title; else null. |
| `IsGamePaused` | Tactical pause / freeze / system pause. |
| `MatchPhaseNeutral` | Enum such as `PreLive`, `Live`, `Intermission`, `PostMatch`, `Unknown` — **not** `freeze` spelled as CS jargon in shared contracts if avoidable. |
| `RoundIndex` | Optional integer for round-based titles; null for MOBA. |

**Rule:** Envelopes schedule against **`GameClock`** + offsets, not wall clock alone, so pauses do not skew ramps.

## Neutral signal examples

| Signal | Type | Notes |
|--------|------|-------|
| `EngagementPressure` | `float 0..1` | Graded fight / combat tension. |
| `ObjectivePressure` | `float 0..1` | Bomb / objective proximity. |
| `SpectatorOrObserver` | `bool` | If true, adaptive music may be disabled by policy. |
| `TransportIntentNeutral` | enum | e.g. `NoChange`, `PreferPause`, `PreferResume`, `PreferSilence`. |

CS2 plugin maps `RoundModule`, bomb DTOs, etc. → these signals. Dota plugin maps its GSI → same shapes.

## TitlePlugin contract (conceptual)

```
OnSnapshot(prev, current, clock) → {
  domainEvents?,
  neutralSignalUpdates,
  safetyFacts
}
```

Implementation language may vary; the **data** crossing into shared music engine must be neutral.

## Type reference (Core)

See `Core/Music/GameClockSnapshot.cs` and related types in `Core/Music/`.
