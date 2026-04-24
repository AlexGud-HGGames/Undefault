# Documentation

Long-form documentation for UndefaultIt lives in this folder. The [repository README](../README.md) stays the quick start: run commands, endpoint table, and configuration cheat sheet.

## Guides

| Document | What it covers |
|----------|----------------|
| [Backend architecture](backend-architecture.md) | End-to-end pipeline (GSI → rules → actions), Spotify control path, HTTP API, config files, detector and rules engine behavior |
| [CS2 GSI events](cs2-gsi-events.md) | Practical CS2 signal space vs current mapping; ideas for future profile rules |
| [CS2 GSI simulator](cs2-simulator.md) | Local console + scenarios library that posts realistic CS2 payloads to `POST /gsi` for development and testing |
| [Roadmap](roadmap.md) | Product direction and planned work |
| [Quick launch](quick-launch.md) | Mock/skip-based startup for local testing and failure handling |
| [Music safety state](music-safety-state-spec.md) | Authoritative `Unknown` / `Safe` / `Danger` contract, dominance, hysteresis |
| [Failure safety](failure-safety-spec.md) | Stale GSI, Spotify failures, degraded device, conservative defaults |
| [Volume composition](volume-composition-spec.md) | Normative merge algebra for transport vs gain vs clamps |
| [Stability & device layer](stability-and-device-layer-spec.md) | Evaluation vs device tick, coalescing, emergency danger lane |
| [Neutral signals & GameClock](neutral-signals-and-game-clock.md) | Cross-title signals and clock contract |
| [Mandatory CS2 ingestion](mandatory-cs2-ingestion-checklist.md) | Required inputs before music controller work |
| [MVP priorities](mvp-priorities-and-confirmation.md) | Documented v1 defaults and deferrals |
| [Ingestion spec (CS2 / Dota)](ingestion-spec-cs2-dota.md) | Snapshot extensions and future Dota shape |
| [Mixer contract & wiring](mixer-contract-and-device-wiring.md) | `IMusicMixer` and Spotify integration boundaries |
| [Music engine config v1](music-engine-config-schema-v1.md) | Options + observability snapshot schema |
| [Rules engine migration](rules-engine-migration.md) | Single orchestration entry, no double Spotify side effects |

## In-repo pointers

- **Host entry and routes:** `GsiHost/Program.cs`
- **GSI processing:** `GsiHost/Services/GsiProcessingService.cs`
- **Console bootstrap:** `GsiHost/Services/ConsoleLaunchBootstrap.cs`
- **Per-area notes:** `GsiHost/Endpoints/README.md`, `GsiHost/Middleware/README.md`, `Core/Spotify/README.md`
