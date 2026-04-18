# Volume Composition Spec — single merge algebra

This document fixes **one** normative pipeline so mixer output is reproducible and testable. **Priority without a formula is forbidden.**

## Intent classes

| Class | Examples | Merge role |
|-------|----------|------------|
| **Transport** | pause, resume, play URI | Resolved by **strict precedence** table — never `max()` with gain. |
| **Gain** | defusal tension multiplier, freeze envelope, UI master % as gain | Combined by **canonical numeric pipeline** below. |
| **Hard override** | emergency mute, `Danger` suppression | **Wins** over gain; interacts with transport per precedence. |
| **Floor / ceiling** | global floor %, max device % | Applied as **clamps** after gains unless `Danger` forbids floor. |

## Transport precedence (highest wins — fixed order)

Evaluate top-down; first match stops for conflicting transport:

1. `Danger` / emergency suppression → **suppress** (pause or volume-to-zero per [floor semantics](music-engine-config-schema-v1.md#floor-vs-danger)).
2. User explicit transport (e.g. “hard stop”).
3. Session transport (e.g. round-boundary pause).
4. Default / no-op → leave transport unchanged from last applied device state.

Document exact mapping to `ISpotifyClient` calls in implementation; this spec only fixes **decision order**.

## Canonical gain pipeline (normative)

Let:

- `B` = base device volume before this frame’s adaptive mix **or** last known playback volume (choose one source of truth in implementation and document it).
- `g₁…gₙ` = gain contributions in `(0, 1]`, each tagged with priority `p` for **tie-break only**.

**Formula:**

```
raw = B * (Πᵢ gᵢ)
effective = Clamp(Floor, Ceiling, raw)
```

- `Floor` / `Ceiling` come from policy (`Unknown`/`Danger` may force Floor=0 and Ceiling=0).
- If **no** gain intents apply, `Π` is treated as **1**.

### Tie-break when two gains conflict at same priority

Sort by stable `(priority DESC, channelId ASC)` and **drop** lower-priority gains that are incompatible if ever needed; for v1, all gains multiply and **logging** warns if any `g < g_min` would explode dynamic range.

### Worked example (same frame)

- `B = 80`
- Defusal gain `g_defuse = 0.9` (tension rising)
- Freeze envelope gain `g_freeze = 0.7`
- UI master `g_ui = 0.5`

`raw = 80 * 0.9 * 0.7 * 0.5 = 25.2` → clamp to `[Floor, 100]`.

If `MusicSafetyState = Danger`, skip this pipeline for audibility: apply suppression transport row instead.

## Floor vs Danger

See [music-engine-config-schema-v1.md](music-engine-config-schema-v1.md). Floor is **not** a safety mechanism: in `Danger`, floor may be **disallowed** so “playing at 5%” cannot violate suppression.

## Golden tests

Golden vectors: fixed `(B, g…, safety, transport)` → expected `(effectiveVolume, transportCommand)`. Tests must not invent alternate algebra.
