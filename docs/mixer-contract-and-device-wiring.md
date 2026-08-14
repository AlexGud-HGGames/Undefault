# Mixer contract and device wiring

> **Status (2026-08-14):** Later work. Tauon MVP talks to `IMusicPlayer` from control-profile actions, not from the mixer. See [music-provider-architecture.md](music-provider-architecture.md).

## Responsibilities

| Component | Input | Output |
|-----------|--------|--------|
| `IMusicMixer` | Classified `AudioIntent` list + `MusicSafetyState` + clamps | `MergedAudioOutput` (target volume %, transport command) |
| `IDeviceCommandCoalescer` | `MergedAudioOutput` + last-sent state | Optional skip, or command batch |
| `IEmergencySuppressionGate` | `MusicSafetyState` edge | Bypass coalescer once for suppress |

## Core interfaces

Implemented as **contracts** in `Core/Music/`:

- `IAudioIntent` — marker; concrete intent records implement it.
- `IMusicMixer` — `Merge(IReadOnlyList<IAudioIntent> intents, MusicMixerContext context)`.

## Interaction with existing Spotify code

- Today: `ISpotifyPlaybackControl`, `ISpotifyClient`, `SpotifyPlaybackControlCoordinator`.
- Target: **single** path from `MergedAudioOutput` → coordinator or thin adapter so duck/pause semantics stay centralized.
- `IEventAction` remains for legacy routing until [rules-engine-migration.md](rules-engine-migration.md) completes.

## Preconditions

Implement mixer code only after [volume-composition-spec.md](volume-composition-spec.md) is frozen for v1.
