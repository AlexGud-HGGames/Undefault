# Hand-off packs (release / dev-experience axis)

These prompts cover the **release & developer-experience axis** of Undefault.
They are intentionally disjoint from the multi-game scenarios axis
(UND-37 and its sub-issues), which is owned by separate agents.

| Pack | File | Issues | Type | Depends on |
|---|---|---|---|---|
| #1 | [`agent-1-research-foundations.md`](agent-1-research-foundations.md) | UND-30, UND-34 | parallel research / docs | — |
| #2 | [`agent-2-release-design-and-packaging.md`](agent-2-release-design-and-packaging.md) | UND-31, UND-32, UND-35 | sequential design + small prototype | pack #1 (UND-30, UND-34) |
| #3 | [`agent-3-ci-implementation.md`](agent-3-ci-implementation.md) | UND-33 | implementation (GitHub Actions) | pack #1 (UND-30) and pack #2 (UND-31) |

Recommended sequencing:

1. Pack #1 in parallel (research only).
2. Pack #2 sequentially (UND-31 → UND-32 → UND-35).
3. Pack #3 last.

Each pack is self-contained: it includes product framing, hard invariants,
required reading, an explicit out-of-bounds list (UND-37 territory), and
per-issue acceptance criteria. Hand any single file to its agent without
extra setup.

Out of scope for every pack:

- `Core/Adapters/*`, `Core/Music/*`, `Core/Rules/*`, `GsiHost/Adapters/Cs2GameAdapter.cs`, `GsiHost/Services/GsiProcessingService.cs`.
- `docs/multi-adapter-routing.md`, `docs/rules-engine-migration.md`, `docs/neutral-signals-and-game-clock.md`, `docs/ingestion-spec-cs2-dota.md`, `docs/volume-composition-spec.md`.
- Linear issues under UND-37: UND-40, UND-41, UND-44, UND-45, UND-43, UND-42, UND-14, UND-18, UND-5, UND-15, UND-16, UND-17.
