# MVP priorities (documented defaults)

**Author note:** Product owner can override. The **current** product MVP is the Tauon automation pivot ([product-pivot-2026-08-14.md](product-pivot-2026-08-14.md)), not the safety-mixer slice below.

**Active implementation order:** [roadmap.md](roadmap.md) `PIVOT-*` tasks. Do not treat the Safety → Mixer → Coalescing sequence in this file as the next coding order.

## Sign-off — 2026-08-14 (Tauon pivot)

1. **Product:** game-aware music automation layer; Undefault does not own the catalog.
2. **First player:** Tauon remote HTTP. Spotify is not a product backend ([spotify-constraints.md](spotify-constraints.md)).
3. **First rules:** `round_start → resume`, `death → pause`. No `round_end` in this slice.
4. **Safety/mixer live path:** deferred. Shadow facade may stay.
5. **Implementation:** only after docs/roadmap (`PIVOT-*`) and an explicit build request.

## Historical / deferred — safety-mixer v1 order

The manifesto safety-first order below is **historical**. It is a later music-engine sequence after the Tauon pivot, not current build guidance. Active work follows `PIVOT-*` in [roadmap.md](roadmap.md).

### Confirmed defaults for v1 (deferred)

1. **Title:** CS2 only.
2. **Order of work (deferred):** Safety specs → ingestion extensions for checklist → neutral clock in observation → mixer + coalescing + emergency lane → one linear envelope → debug snapshot HTTP.
3. **Scenario priority for first playable slice:** **Safety + stale input + emergency suppression** before defusal tension curves.
4. **Defusal vs freeze vs floor:** Implement **failure + danger path** first; then **floor semantics** (single table); then **freeze linear envelope**; then **defusal gain** as multiplier in volume spec.

## Deferred (post-v1)

- Full Dota plugin
- Envelope queues, ADSR
- Rich replay/spectator clock edge cases

## Sign-off

When the product owner confirms different priorities, append a short dated section below.

---
*Defaults recorded as part of manifesto implementation.*
