# Multi-adapter routing on a single host

## Purpose

`GsiHost` today registers exactly one `IGameAdapter<GsiPayloadDto>`
(`Cs2GameAdapter`) and exposes a single `/gsi` endpoint that hard-codes
the CS2 payload DTO. Adding a second title (Dota 2 first, possibly more
later) without forking the host requires choosing how the host accepts
and dispatches multiple title payloads.

The adapter boundary in `Core/Adapters/` (`IGameAdapter<TPayload>`,
`AdapterObservation`, `NeutralContext`, `SafetyFacts`) was already built
generic on purpose. Only the host wiring is title-specific.

This doc records the spike, the chosen option, and the small refactor
that ships with UND-40 so future title work does not have to change the
host shape.

## Constraints

- **CS2 baseline must not regress.** All 156 tests on `main` must stay
  green and `POST /gsi` for CS2 GSI payloads must behave identically.
- **One Spotify side-effect path per `/gsi` tick.** Whatever routing
  shape is chosen, each tick must still flow through one
  `GsiProcessingService` call chain with one `IRulesEngine` evaluation
  and (post-UND-44) one `IMusicOrchestrationFacade` dispatch.
- **Spotify and OAuth state stays shared across titles.** The Spotify
  account is one Spotify account regardless of which game posted the
  tick; `ISpotifyClient`, `ISpotifyPlaybackControl`,
  `ITokenStorage`, and `IMusicOrchestrationFacade` must remain process
  singletons used by every adapter.
- **CS2 first.** Dota and any other title come later. The chosen shape
  must allow Dota to be added by registering a new adapter, not by
  touching CS2 paths.
- **Adapters are typed.** Each title has its own raw payload DTO
  (`GsiPayloadDto` for CS2, a future `DotaPayloadDto` for Dota).
  Routing must preserve typed deserialization rather than collapsing
  everything into `JsonElement`.

## Options considered

### Option A — Parallel host process per title

A separate `GsiHost` process per game (one for CS2, another for Dota),
each binding its own port and only knowing about its own adapter.

| Dimension | Notes |
|---|---|
| Routing complexity | Trivial in code; complex out-of-process. Two binaries to launch, two console checklists, two startup sequences. |
| Simulator fit | Bad. The CS2 simulator and any future Dota simulator would need to know which port to target; tester scenarios cannot mix titles. |
| OAuth / Spotify state sharing | Worst. Spotify token storage is process-local today; two processes mean two OAuth flows for the same Spotify account, doubled refresh handling, and a real risk of two processes writing playback at the same time (breaks the "one Spotify side-effect per tick" invariant across the product, even if each process keeps it within itself). |
| Per-title configuration | Naturally separated (each process has its own `appsettings.json`). Cost: shared sections (Spotify, control profiles, mixer) duplicate. |
| Dota appid availability | Irrelevant — port-based separation. |

### Option B — Per-title HTTP endpoint with its own typed DTO

Single `GsiHost` process. Each title gets its own endpoint
(`/gsi` for CS2, `/gsi/dota` for Dota) with its own typed minimal-API
handler, its own DTO, and its own `IGameAdapter<TPayload>`
implementation. All endpoints share the rules engine, facade, and
Spotify client through DI.

| Dimension | Notes |
|---|---|
| Routing complexity | Lowest. ASP.NET Minimal API maps each endpoint to a typed handler. No JSON peeking, no runtime dispatch table, no risk of one title's DTO leaking into another's parser. |
| Simulator fit | Best. Each simulator targets its title's URL. Tester tools opt into a single title trivially. |
| OAuth / Spotify state sharing | Trivial. One process, one DI container, one `ISpotifyClient`, one token storage, one facade. |
| Per-title configuration | Each title can carry its own options section (`Cs2`, `Dota`, …) without colliding. |
| Dota appid availability | Not required at runtime; appid lives only in metadata for diagnostics. |
| Drawbacks | Users (or game cfg generators) must point each title's GSI cfg at the right URL. Mitigated by the per-title setup helpers (`Cs2SetupService` writes the CS2 cfg today; a future `DotaSetupService` would do the same for Dota). |

### Option C — Single `/gsi` router by `provider.appid`

Single `/gsi` endpoint that reads the raw JSON, peeks at
`provider.appid` (730 = CS2, 570 = Dota), and dispatches to the right
typed adapter.

| Dimension | Notes |
|---|---|
| Routing complexity | Highest. Either parse twice (peek + typed) or carry a custom converter that branches by appid. Error handling for missing/unknown appid becomes a runtime concern instead of a 404 from the framework. |
| Simulator fit | Same as B once `provider.appid` is correctly set; subtly worse for tooling that wants to feed the host arbitrary payload shapes for negative testing. |
| OAuth / Spotify state sharing | Same as B. |
| Per-title configuration | Same as B (configuration sections are independent of routing). |
| Dota appid availability | The product depends on Dota actually emitting `provider.appid`. CS2 GSI emits it reliably; Dota 2 GSI is documented to emit `provider.appid = 570`, but the project has not validated that contract on a live Dota client. Building runtime routing on an unverified producer field is a risk. |
| Drawbacks | Couples all titles to a single endpoint, introduces a JSON peek + redirect step, and requires a fallback for missing/unknown appid that has to be exercised by tests. |

## Decision

**Adopt Option B (per-title HTTP endpoint with its own typed DTO).**

Rationale: Option B is the simplest design that satisfies every
constraint. Each adapter stays strongly typed end to end, the host
keeps using ASP.NET Minimal API as designed, the Spotify and OAuth
singletons are naturally shared across titles, and adding Dota means
"register a new typed adapter and a new endpoint" — no JSON peeking,
no runtime appid contract to defend, no per-process duplication. The
small operational cost (each title's GSI cfg points at its own URL)
is already paid by the existing `Cs2SetupService`, which can be
mirrored per title.

Option C remains available as a future evolution if multiple titles
ever need to share a single endpoint (e.g. for an external aggregator
that cannot rewrite cfg files), but it is not needed for Dota.

Option A is rejected outright: it breaks Spotify/OAuth state sharing
and the product-level "one Spotify side-effect per tick" invariant.

## Refactor landed with this issue

Per the AC ("introduce an `IGameAdapterRouter` interface even if it
has one implementation today"), this issue lands a small registration
shape that makes adding a second adapter mechanical, without changing
CS2 behavior.

- `Core/Adapters/GameAdapterRegistration.cs` — record
  `(string TitleId, int? AppId, string EndpointPath, string Description)`.
  Carries the metadata each adapter must declare to participate in
  multi-title routing.
- `Core/Adapters/IGameAdapterRouter.cs` and the default
  `GameAdapterRouter` — registry of `GameAdapterRegistration` with
  lookup by endpoint path and by `AppId`. Today exactly one CS2 entry
  is registered. Dota would add a second entry without touching the
  CS2 code path.
- `GsiHost/Program.cs` — registers the CS2 registration explicitly
  alongside the existing typed adapter binding, and exposes the
  registry read-only at `GET /diagnostics/adapters` so testers and
  future docs can inspect which titles a given host serves.
- `GET /gsi` (CS2) and the CS2-typed `GsiProcessingService` are
  unchanged. They keep using
  `IGameAdapter<GsiPayloadDto> = Cs2GameAdapter`.

When a Dota adapter is added (UND-43 follow-up), the additions look
like:

```csharp
builder.Services.AddSingleton<IGameAdapter<DotaPayloadDto>, DotaGameAdapter>();
builder.Services.AddSingleton(new GameAdapterRegistration(
    TitleId: "dota2",
    AppId: 570,
    EndpointPath: "/gsi/dota",
    Description: "Dota 2 GSI"));

app.MapPost("/gsi/dota", async (DotaPayloadDto payload, /* per-title processing service */, CancellationToken ct) => { ... });
```

No CS2 code path changes; no router-by-appid logic to maintain;
diagnostics list both registrations automatically.

## Out of scope

- The Dota adapter implementation itself (tracked as a follow-up
  issue under UND-37 once Dota work begins).
- Per-title `GsiProcessingService` factoring. `GsiProcessingService`
  is currently typed on `GsiPayloadDto`; making it generic on payload
  type is a small follow-up that can land with the first non-CS2
  adapter, not now (CS2 baseline must stay byte-for-byte identical
  for UND-40).
- UI for selecting active title. The host serves all registered
  titles concurrently; selection is per-game-cfg, not in-app.

## Cross-references

- [Backend architecture](backend-architecture.md) — HTTP surface
  section names the chosen model and lists `/diagnostics/adapters`.
- [Ingestion spec — CS2 (v1) and Dota 2 (future)](ingestion-spec-cs2-dota.md)
  — Dota subsection points at this doc for the routing decision.
- [Rules engine migration](rules-engine-migration.md) — single
  orchestration entry per tick; Option B preserves that invariant
  per endpoint.
