# MusicSafetyState — authoritative safety contract

This document is the **single source of truth** for gameplay-safe music behavior. Adaptive music (defusal curves, freeze envelopes, floor volume) is **subordinate** to this contract.

## States

| State | Meaning |
|-------|---------|
| `Unknown` | Cannot prove that music is safe to be audible at full/adaptive levels. |
| `Safe` | Game observation + clock support the conclusion that adaptive music may run under configured policies. |
| `Danger` | Music must be suppressed (or held at a configured danger floor) per product policy — **dominates** all other layers. |

**Invariant:** `Danger` overrides every envelope, mixer channel, control profile, and track-routing action. `Unknown` is **conservative**, not optimistic: it must not behave like `Safe` for audibility decisions.

## Ownership

- **Owner:** a dedicated **safety controller** (conceptual component; implementation may live in Core and/or GsiHost behind a single facade).
- **Inputs:** `safetyFacts` from title plugins (e.g. CS2), `GameClock`, observation staleness, and explicit policy flags.
- **Outputs:** `MusicSafetyState` each evaluation tick, plus optional **emergency device commands** (see [stability-and-device-layer-spec.md](stability-and-device-layer-spec.md)).

The mixer and music session controller **read** safety state; they do not **vote** on it.

## Dominance rules

1. If `Danger` → adaptive session logic must not increase audibility; envelopes in flight are **cancelled** or frozen at safe-attenuation.
2. If `Unknown` → treat as **non-safe** for adaptive loudness: default is suppressed or minimum safe floor (per Failure Safety Spec).
3. If `Safe` → adaptive layers may run subject to [volume-composition-spec.md](volume-composition-spec.md) and title-specific rules.

## Transition table (normative shape)

Concrete thresholds are **configuration**; the **structure** is fixed.

### Entering `Danger` (preemptive, fast path)

Transition to `Danger` when **any** configured danger predicate becomes true, including examples:

- Bomb planted and policy marks post-plant as danger for the local player role.
- Tactical pause / freeze boundary where product requires hard silence.
- Observation staleness beyond `T_stale` (see [failure-safety-spec.md](failure-safety-spec.md)) — may force `Unknown` first, then `Danger` if policy says stale ⇒ danger.

**Asymmetry:** `Safe → Danger` is **immediate** at the decision layer. Device apply uses the **emergency lane** (bypass coalescing for suppression — see stability spec).

### Entering `Unknown`

- No fresh observation within `T_stale`.
- Contradictory signals in one frame unresolved by [single-tick rules](#single-tick-conflicts).
- Spotify / device in a degraded mode where “safe” cannot be asserted (per failure spec).

### Entering `Safe`

Only when **explicit** safe predicates hold, e.g.:

- Live round, not in danger window, player active (not spectator if policy requires), inputs fresh.
- Hysteresis for exit from `Danger` satisfied (below).

### Leaving `Danger` (slow path, hysteresis)

Do **not** mirror enter semantics. Exit to `Safe` only if:

1. No danger predicate true for **`H_danger` consecutive evaluation ticks** or **`H_danger_ms` on GameClock`**, and  
2. Fresh observation confirms safe context.

Exit to `Unknown` if observation becomes ambiguous before `Safe` is proven.

### Leaving `Unknown`

Transition to `Safe` only when fresh observation + rules say so; otherwise remain `Unknown` or escalate to `Danger` per policy.

## Single-tick conflicts

When one GSI frame implies both routine adaptive updates and `Danger`:

1. Evaluate **safety first**.
2. If result is `Danger`, discard or defer non-essential adaptive work for that frame.
3. Log merge reason in debug snapshot (see [music-engine-config-schema-v1.md](music-engine-config-schema-v1.md)).

## Relationship to other specs

| Spec | Role |
|------|------|
| [failure-safety-spec.md](failure-safety-spec.md) | Stale input, API failures, degraded device |
| [volume-composition-spec.md](volume-composition-spec.md) | How safe adaptive intents combine numerically |
| [stability-and-device-layer-spec.md](stability-and-device-layer-spec.md) | Emergency lane vs coalescing |

## Type reference (Core)

See `Core/Music/MusicSafetyState.cs` for the enum used by code and DTOs.
