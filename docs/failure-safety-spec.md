# Failure Safety Spec — stale input, API, and device behavior

This document defines **conservative** behavior when game data or Spotify is unreliable. It complements [music-safety-state-spec.md](music-safety-state-spec.md).

## Honest scope: Spotify Web API

The architecture can **prefer** immediate suppression and **attempt** urgent API calls, but **cannot** guarantee hard real-time silence in all conditions: network latency, token/device errors, and missing active playback device are real.

**Product wording:** define what **“immediate”** means as an **engine-side SLA** (e.g. “suppression command issued within **≤ 50 ms** of `Danger` decision on evaluation tick”) vs **“audible silence achieved”** (depends on Spotify and OS).

## Stale game observation

| Parameter | Description |
|-----------|-------------|
| `T_stale` | Max age of last accepted observation (wall or game clock per config). |

**Rule:** If `now - lastObservationTimestamp > T_stale` → `MusicSafetyState = Unknown` (conservative). If policy `staleEscalatesToDanger` is true → `Danger`.

**Rule:** `Unknown` must **not** be treated as `Safe` for loudness. Default audibility: **suppressed** or **minimum safe floor** only if explicitly allowed for `Unknown` in product config.

## Missing / ambiguous events

- Absence of confirming **safe** signals does **not** imply safe.
- If a frame is ambiguous after deterministic ordering (see music safety spec), prefer `Unknown` or `Danger` per stricter policy flag.

## Spotify API rejection

On non-success or exception from a **suppression** call:

1. Set device fault flag in [debug snapshot](music-engine-config-schema-v1.md).
2. Re-attempt suppression up to `N_retry` with backoff **only for non-emergency** paths; emergency lane may allow **one** immediate retry at policy discretion.
3. Keep `MusicSafetyState` at `Danger` (or `Unknown` if engine cannot assert command acceptance).

## No active playback device

- If `GetCurrentPlaybackAsync` returns null / no device when suppression is required:
  - Surface **degraded** mode in debug snapshot.
  - Policy options (pick one as default in implementation):  
    - **A:** Remain `Danger` / `Unknown` until device available (conservative).  
    - **B:** Disable adaptive music entirely until device present.

Document the chosen default in `MusicEngineOptions` v1.

## Conservative default summary

| Condition | Default safety posture |
|-----------|-------------------------|
| Stale input | `Unknown` (or `Danger` if configured) |
| Unknown semantics | Not safe for full adaptive audio |
| API failure on suppress | `Danger` intent retained; fault visible |
| No device | Degraded + conservative audibility |

## Type reference (Core)

`Core/Music/MusicEngineDebugSnapshot.cs` includes fields for desired vs last-known device compliance when implemented.
