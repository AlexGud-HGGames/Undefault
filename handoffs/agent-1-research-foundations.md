# Hand-off #1 — Research foundations (UND-30 + UND-34)

You are taking over the **research foundation** of Undefault's release & developer-experience axis. Two parallel research issues only — no implementation, no refactors, no commits to product code. Output is a written report per issue.

This work is **disjoint from the multi-game scenarios axis (UND-37 and its sub-issues)** — that track is being driven by other agents. Do not touch UND-37 territory (see the "Out of bounds" section).

## Product framing (do not violate)

- Undefault is a **local Spotify playback controller** that reacts to game state. Not a synchronized soundtrack, not a way around Spotify rules.
- CS2 is the first title; Dota and others come later (handled by the UND-37 track).
- **Safety dominates adaptivity.** `MusicSafetyState.Danger` overrides any positive playback intent.
- **Exactly one Spotify side-effect path per `/gsi` tick** (existing invariant, preserved by all tracks).
- `Hotkeys + Timeline` is **tester / product-owner-only** intent-capture tooling.
- **No secrets in repo, artifacts, or logs.** This is the explicit guardrail UND-34 must defend.

## State of the world (already on `main`)

- 163 tests green: Core 60, GsiHost 59, Cs2Simulator 44.
- TFM mix today: most projects target `net8.0`; **only `Core.Tests` targets `net9.0`**. This is exactly UND-30's input.
- Spotify OAuth lives in `Core/Spotify/SpotifyOAuthService.cs` + `Core/Spotify/SpotifyClient.cs`; tokens live in `Core/Spotify/InMemoryTokenStorage.cs` (process-local). Console secret precedence (env → encrypted Windows store → `appsettings.json` → prompt) lives in `GsiHost/Services/ConsoleLaunchBootstrap.cs`. This is exactly UND-34's input.

## Working agreement

- **Linear is the source of truth.** Use the `plugin-linear-linear` MCP tools. Workspace `undefault`, team `Undefault`. AC wins over description.
- **No PRs, no feature branches.** The user works directly on `main`.
- **No git commits unless the user explicitly asks.** Show the proposed diff/doc, wait for go-ahead, then commit.
- **Status discipline.** Move issue from `Backlog` → `Todo` → `In Progress` before starting and post a brief plan comment. Move to `Done` only after the report is approved by the user; on Done, drop a one-comment summary with the doc path (and commit SHA if a doc landed).
- **No implementation, no refactor.** Both issues in this pack are **research-only**. Do not edit `*.csproj`, do not change OAuth code, do not create follow-up Linear issues for implementation without explicit user approval.

## Hard invariants you must not put at risk

1. CS2 baseline behaviour is byte-for-byte identical: `POST /gsi` round_start ducks, death restores, all 163 tests stay green. (You are not changing code — these stay green by default.)
2. Manual intent (`POST /user-actions`, Windows hotkeys) keeps using `ISpotifyPlaybackControl` directly.
3. **No secrets in repo, artifacts, or logs.** Includes Spotify `client_secret`, refresh tokens, access tokens, and the encrypted store path's contents. UND-34's recommendation must defend this guardrail explicitly.

## Required reading

- `docs/README.md` — doc index.
- `docs/backend-architecture.md` — host topology, console bootstrap, persistence services, Spotify runtime modes.
- `docs/spotify-developer-compliance-notes.md` — Spotify Developer Terms, Policy, Compliance Tips with concrete guardrails.
- `docs/spotify-playback-policy-boundary.md` — local Spotify control, not synchronized soundtrack.
- `docs/quick-launch.md` — mock/skip-based startup useful for thinking about test/dev modes.
- `*.csproj` files at the repo root and per project — current TFM mix.
- `GsiHost/Services/ConsoleLaunchBootstrap.cs` — current secret precedence and credential lifecycle.
- `Core/Spotify/SpotifyOAuthService.cs`, `Core/Spotify/SpotifyClient.cs`, `Core/Spotify/InMemoryTokenStorage.cs` — OAuth and token shape today.

## Out of bounds (do not touch — UND-37 territory)

- `Core/Adapters/*`, `Core/Music/*`, `Core/Rules/*`, `GsiHost/Adapters/Cs2GameAdapter.cs`, `GsiHost/Services/GsiProcessingService.cs`, `docs/multi-adapter-routing.md`, `docs/rules-engine-migration.md`, `docs/neutral-signals-and-game-clock.md`, `docs/ingestion-spec-cs2-dota.md`, `docs/volume-composition-spec.md`. Read freely; modify only with explicit user permission.
- Open Linear issues under UND-37: UND-40 (in flight), UND-41, UND-44, UND-45, UND-43, UND-42, UND-14, UND-18, UND-5, UND-15, UND-16, UND-17. Do not touch their status, comments, or scope.

## Issue order — both can run in parallel

### 1. UND-30 — Research: unify .NET SDK (net8 vs net9 across tests)

Linear: <https://linear.app/undefault/issue/UND-30>

Pure research. Output is a short written report.

Scope:

- Walk every `*.csproj` in the solution and list its `<TargetFramework>` (or `<TargetFrameworks>`).
- Note which projects are libraries vs apps vs test projects.
- List the risks of staying mixed (`net8.0` everywhere except `Core.Tests` on `net9.0`): runtime duplication, package compatibility, CI complexity, tooling surface, future maintenance.
- Compare the two realistic alignments: align everything on `net8.0` (LTS) vs align everything on `net9.0`. Note any blockers (test SDK requirements, downstream package constraints, ASP.NET Core version coupling).
- Recommend a TFM (or explicitly recommend staying mixed with a written justification).
- List clear follow-up criteria so a separate implementation issue can be filed if the recommendation is to migrate.

Deliverable:

- Either a comment on UND-30 with the full report, or a new doc at `docs/dotnet-tfm-decision.md` if the report is long enough to deserve a permanent home. Prefer the doc.
- Do **not** refactor `*.csproj`. Do **not** create the implementation follow-up issue without the user's go-ahead.

Acceptance:

- Current TFM state is enumerated per `*.csproj`.
- Risks of the current mix are listed.
- A specific TFM recommendation (or "stay mixed, here's why") is given.
- Follow-up criteria are written so a future implementation issue is mechanical to file.

When done: present the report to the user. After approval, commit the doc to `main` (if you wrote one), push, move UND-30 to `Done` with a one-comment summary linking the doc / commit SHA.

### 2. UND-34 — Spotify OAuth & secrets model for dev/test/prod

Linear: <https://linear.app/undefault/issue/UND-34>

Pure research / design. Output is a written model the team can implement against later.

Scope:

- Recommend an OAuth flow for a Windows desktop / local client: PKCE without client secret vs traditional Authorization Code with client secret. Cite the relevant Spotify Developer guidance from `docs/spotify-developer-compliance-notes.md` and the official docs.
- Define how many Spotify Developer app registrations the project needs and why (local dev, internal test, future prod). State which `client_id`s can be embedded in a build vs which must come from env / encrypted store.
- Redirect URI strategy: localhost callback, fixed port vs random port, custom URI scheme. Address Windows constraints (firewall, port collisions, SmartScreen impact).
- Token storage: today `InMemoryTokenStorage` is process-local (tokens vanish on restart). Recommend whether to keep that for first tester builds or to add a persistent encrypted store, and what `logout` / revocation should mean.
- CI secrets: which values live in GitHub Actions secrets, which in local developer config, which in the tester's encrypted store. **Nothing OAuth-secret may be embedded in the artifact or logs.**
- Security checklist that the release pipeline (UND-31) and packaging (UND-32) work must enforce.

Deliverable:

- New doc at `docs/spotify-oauth-secrets-model.md` (preferred) or a long comment on UND-34. Prefer the doc.
- A list of suggested follow-up implementation issues (e.g. "switch to PKCE", "add persistent encrypted token store", "rotate test app credentials") **suggested but not created** until the user approves.
- **Do not change OAuth code in this issue.** Reference `ConsoleLaunchBootstrap`, `SpotifyOAuthService`, `SpotifyClient`, `InMemoryTokenStorage` only.

Acceptance:

- Recommended OAuth flow is named with a one-paragraph justification grounded in Spotify guidance.
- Per-environment app registration plan is written.
- Redirect URI strategy is concrete (specific port choice or rationale for random).
- Token storage recommendation is concrete (keep in-memory vs add encrypted persistent store) with rationale.
- CI / local secret split is enumerated.
- Security checklist is enumerated and ready for UND-31 / UND-32 to consume.
- Follow-up implementation tasks are listed as suggestions, not created in Linear.

When done: present the doc to the user. After approval, commit to `main`, push, move UND-34 to `Done` with a summary comment linking the doc and commit SHA.

## Definition of done for this hand-off

You are done when:

- UND-30 and UND-34 are both `Done` in Linear with their docs on `main` and pushed.
- Hand-off pack #2 (release design + packaging + checklist) and #3 (CI implementation) have the inputs they need: a TFM choice for CI SDK setup (UND-30) and a secrets policy for what CI / artifacts may carry (UND-34).

## Start here

1. Read all docs under "Required reading".
2. Open UND-30 and UND-34 in Linear; move both to `In Progress`; post a brief plan comment in each.
3. Research in parallel; write the docs.
4. Present each doc to the user, **wait for explicit go-ahead before committing**, then commit to `main`, push, move the issue to `Done` with a one-comment summary containing the doc path and commit SHA.

If anything blocks you (missing AC, contradiction with workspace rules, hard-invariant risk, drift into UND-37 territory), stop and ask the user. Do not silently expand scope.
