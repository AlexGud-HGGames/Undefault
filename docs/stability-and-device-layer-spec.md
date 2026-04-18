# Stability and device layer — evaluation tick, coalescing, emergency lane

## Two clocks

| Layer | Cadence | Responsibility |
|-------|---------|----------------|
| **Evaluation tick** | Every GSI frame and/or fixed timer (e.g. 50 Hz max in-process) | Recompute `MusicSafetyState`, session, envelopes, **merged** target volume / transport **intent**. No Spotify I/O. |
| **Device tick** | Only when needed | Apply to Spotify with coalescing and rate limits. |

## Coalescing (normal path)

- **ε-volume:** if `|v_new - v_last_sent| < ε_vol`, skip send.
- **Min interval:** do not send volume APIs faster than `T_min_volume` unless emergency lane fires.
- **Idempotency:** if merged command equals last sent, no-op (debug log only).

Suggested defaults (tune in implementation): `ε_vol = 1%`, `T_min_volume = 150 ms` (documentation values, not hardcoded forever).

## Emergency lane (`Safe → Danger`)

**Must bypass** for the **suppression** operation:

- Normal ε and min-interval **do not apply** to the first suppression dispatch after `Danger` edge.
- **Cancel** queued ramps / pending envelope segments for that evaluation generation.
- Issue **immediate** suppress command (pause and/or volume per policy).

**Asymmetry:** `Danger → Safe` uses normal coalescing + hysteresis from [music-safety-state-spec.md](music-safety-state-spec.md).

## “Immediate” definition

- **Engine SLA:** from `Danger` edge detection to **enqueue** of device command: target **≤ 50 ms** on evaluation thread (configurable).
- **Audible result:** not guaranteed; record **desired** vs **acknowledged** state in debug snapshot.

## Spotify failure on emergency path

Allow **at most one** immediate retry (policy); then set fault flags and keep internal state `Danger` until user resolves device/auth.

## Wiring (target)

```
Evaluation → MusicSafetyState + Mixer → MergedDeviceCommand
                ↓ (Danger edge)
         EmergencyBypass → ISpotifyClient / coordinator
                ↓ (else)
         CoalescingLayer → ISpotifyClient / coordinator
```

See [mixer-contract-and-device-wiring.md](mixer-contract-and-device-wiring.md).
