# MVP priorities (documented defaults)

**Author note:** Product owner can override. The **current** product MVP is the Tauon automation pivot ([product-pivot-2026-08-14.md](product-pivot-2026-08-14.md)), not the safety-mixer slice below.

## Sign-off — 2026-08-14 (Tauon pivot)

1. **Product:** game-aware music automation layer; Undefault does not own the catalog.
2. **First player:** Tauon remote HTTP. Spotify is not a product backend ([spotify-constraints.md](spotify-constraints.md)).
3. **First rules:** `round_start → resume`, `death → pause`. No `round_end` in this slice.
4. **Safety/mixer live path:** deferred. Shadow facade may stay.
5. **Implementation:** only after docs/roadmap (`PIVOT-*`) and an explicit build request.

The manifesto safety-first order below remains a **later** music-engine sequence, not the next coding milestone.

## Confirmed defaults for v1

1. **Title:** CS2 only.
2. **Order of work:** Safety specs → ingestion extensions for checklist → neutral clock in observation → mixer + coalescing + emergency lane → one linear envelope → debug snapshot HTTP.
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
