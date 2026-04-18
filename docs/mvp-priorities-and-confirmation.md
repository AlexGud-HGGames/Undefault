# MVP priorities (documented defaults)

**Author note:** Product owner can override; these defaults satisfy the manifesto **safety-first v1** slice when no explicit confirmation is recorded.

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
