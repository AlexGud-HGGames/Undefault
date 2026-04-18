# Music engine config and observability — schema v1

## Versioning

- **`SchemaVersion`**: `1` for this document.
- Host JSON and HTTP DTOs should include `schemaVersion` for forward compatibility.

## MusicEngineOptionsV1 (summary)

| Field | Type | Purpose |
|-------|------|---------|
| `SchemaVersion` | `int` | Must be `1`. |
| `StaleObservationMs` | `int` | `T_stale` for [failure-safety-spec.md](failure-safety-spec.md). |
| `StaleEscalatesToDanger` | `bool` | If true, stale → `Danger` instead of `Unknown`. |
| `DangerExitHysteresisMs` | `int` | `H_danger` window for leaving `Danger`. |
| `FloorVolumePercent` | `int?` | Adaptive floor; ignored when `MusicSafetyState = Danger` if `ForbidFloorInDanger` is true. |
| `ForbidFloorInDanger` | `bool` | Default **true**. |
| `EmergencyEngineSlaMs` | `int` | Target max time from danger edge to command enqueue (default 50). |
| `VolumeEpsilonPercent` | `int` | ε for coalescing (default 1). |
| `MinVolumeCommandIntervalMs` | `int` | Normal path (default 150). |

Full shape: see `Core/Music/MusicEngineOptionsV1.cs`.

## Floor vs Danger {#floor-vs-danger}

| MusicSafetyState | ForbidFloorInDanger | Audibility policy |
|------------------|---------------------|-------------------|
| `Danger` | `true` | Suppression via transport row; **no** “play at floor%”. |
| `Danger` | `false` | Rare product mode: allow quiet-but-audible floor (document UX risk). |
| `Unknown` | — | Conservative: treat like non-safe; typically no adaptive boost. |
| `Safe` | — | Floor applies per [volume-composition-spec.md](volume-composition-spec.md). |

Floor is **UX / adaptive**, not a safety guarantee.

## Observability — MusicEngineDebugSnapshot

Exposed over HTTP in a future endpoint; fields include:

- `MusicSafetyState` (desired)
- `LastSafetyTransitionReason`
- `GameClock` subset
- `MixerChannelContributions` (channel id → partial gain / note)
- `LastMergedVolumePercent`
- `LastDeviceCommands` (ring buffer: action, success, timestamp)
- `DeviceDegraded` / `LastSpotifyError` (non-secret)

See `Core/Music/MusicEngineDebugSnapshot.cs`.

## Related

- [music-safety-state-spec.md](music-safety-state-spec.md)
- [failure-safety-spec.md](failure-safety-spec.md)
