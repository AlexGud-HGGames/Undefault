# Hand-off #3 — CI implementation: build + tests on PR and main (UND-33)

You are taking over the **first concrete CI implementation** for Undefault. Single issue: a GitHub Actions workflow that runs `dotnet restore` / `dotnet build` / `dotnet test` on PR and `main`. No artifact publishing, no signing, no release distribution — those are gated on hand-off pack #2 deliverables and remain as separate future issues.

This work is **disjoint from the multi-game scenarios axis (UND-37 and its sub-issues)** — that track is being driven by other agents. Do not touch UND-37 territory (see "Out of bounds").

You depend on hand-off packs #1 and #2:

- **UND-30** (hand-off #1) — `Done` (research doc landed in `515b42f`). Decision: solution uniformly on `net8.0`. UND-46 implemented it (`09c245d`). CI pins a **single** `8.0.x` SDK channel; **do not** add `9.0.x`.
- **UND-31** (hand-off #2) — decides the artifact-policy boundary — what CI may publish today (default: nothing). Required input. If still `In Progress`, ask the user whether to wait for UND-31's design or to ship a default-conservative CI (single pinned SDK, no artifact publishing, no caching beyond NuGet) and revise later.

## Product framing (do not violate)

- Undefault is a **local Spotify playback controller** that reacts to game state. Not a synchronized soundtrack.
- CS2 is the first title; Dota and others come later (UND-37 track).
- **Safety dominates adaptivity.** `MusicSafetyState.Danger` overrides any positive playback intent.
- **Exactly one Spotify side-effect path per `/gsi` tick** (existing invariant).
- `Hotkeys + Timeline` is **tester / product-owner-only** intent-capture tooling.
- **No secrets in repo, artifacts, or logs.** This applies to CI logs too: the workflow must not echo `CLIENT_ID` or any tokens, must not pass them as positional args, must not write them into uploaded artifacts. (Post-UND-47 the product no longer uses a `client_secret` at all; the rule about not echoing it stays defensively in case anyone ever reintroduces one.)

## State of the world (already on `main`)

- 170 tests green: Core 64 (`net8.0`), GsiHost 62 (`net8.0`), Cs2Simulator 44 (`net8.0`). Solution at the repo root (`UndefaultIt.sln`).
- TFM: solution is uniformly on `net8.0` (UND-46 closed; commit `09c245d`). CI installs a **single** `.NET 8.0.x` SDK channel.
- No CI workflow exists today. No `.github/workflows/` folder.
- No GitHub repository secrets are documented as required for CI yet (this issue does not need any — build + test only).

## Working agreement

- **Linear is the source of truth.** `plugin-linear-linear` MCP tools. Workspace `undefault`, team `Undefault`. AC wins over description.
- **No PRs, no feature branches.** Commit straight to `main` only after explicit user go-ahead.
- **Status discipline.** Issue `Backlog` → `Todo` → `In Progress` before starting; brief plan comment; `Done` only after the workflow is green on `main`; on Done, drop a one-comment summary with the commit SHA and a link to the first green run.
- **Comments policy in code.** Only when they explain non-obvious intent, trade-offs, or safety invariants. No XML doc that restates the symbol name. No "Phase X / UND-NN" narration in code. No TODO without a Linear ID. (Workflow YAML follows the same rule for `# comments`.)
- **Tests first-class.** Run `dotnet build` and `dotnet test` locally before committing. The CI workflow you ship must reproduce the same green outcome on a clean GitHub Actions runner.
- **No silent scope expansion.** Do not add deployment steps, code signing, NuGet publish, Docker builds, GitHub Releases, or auto-update plumbing. Those belong to follow-up issues filed by hand-off pack #2's design.

## Hard invariants you must not put at risk

1. CS2 baseline behaviour is byte-for-byte identical: `POST /gsi` round_start ducks, death restores, all 170 tests stay green (Core 64 + GsiHost 62 + Cs2Simulator 44). The CI run on `main` for the head commit must match the local outcome.
2. Manual intent (`POST /user-actions`, Windows hotkeys) keeps using `ISpotifyPlaybackControl` directly, does not flow through `RulesEngine.ActionMap`. Existing tests cover this; do not weaken or skip them.
3. `intent_capture` runtime mode stays gated. Existing tests cover this; do not weaken or skip them.
4. **No secrets in repo, artifacts, or logs.** The workflow must not require any GitHub repository secrets to run; if any future step ever does, it must use `${{ secrets.* }}` masked and never `echo` them.

## Required reading

- `docs/README.md` — doc index.
- `docs/backend-architecture.md` — what `GsiHost` is, what tests cover.
- `docs/spotify-developer-compliance-notes.md` — secrets are sensitive even in CI logs (refreshed in commit `d9f5de9` post-PKCE; `client_id` and tokens are the live concerns, `client_secret` is not present in the product).
- `docs/dotnet-tfm-decision.md` — already on `main`; required input for SDK setup. Decision: pin a single `8.0.x` SDK channel.
- `docs/spotify-oauth-secrets-model.md` — already on `main`; informs the no-secrets-in-CI-logs rule. Implementation lives in commits `9407ab7` (UND-47 PKCE) and `d9f5de9` (UND-49 cleanup).
- The output of hand-off pack #2: `docs/release-pipeline-design.md` (or its UND-31 comment if UND-31 is still in progress) — required input for the artifact-policy boundary (default: no artifact publishing in this issue).
- Existing `*.csproj`s and the `*.sln` at the repo root.
- `Cs2SetupTestCollection.cs` and any test that touches the file system — confirm tests are CI-safe (no Windows-only paths assumed; if any are, scope a follow-up issue rather than weakening the test).

## Out of bounds (do not touch — UND-37 territory)

- `Core/Adapters/*`, `Core/Music/*`, `Core/Rules/*`, `GsiHost/Adapters/Cs2GameAdapter.cs`, `GsiHost/Services/GsiProcessingService.cs`, `docs/multi-adapter-routing.md`, `docs/rules-engine-migration.md`, `docs/neutral-signals-and-game-clock.md`, `docs/ingestion-spec-cs2-dota.md`, `docs/volume-composition-spec.md`. Read freely; modify only with explicit user permission.
- Open Linear issues under UND-37: UND-40, UND-41, UND-44, UND-45, UND-43, UND-42, UND-14, UND-18, UND-5, UND-15, UND-16, UND-17.

## Issue

### UND-33 — CI: automated build + tests on PR and main

Linear: <https://linear.app/undefault/issue/UND-33>

Implementation. Add a GitHub Actions workflow.

Scope:

- Workflow file at `.github/workflows/ci.yml` (or similar).
- Triggers: `pull_request` (any branch targeting `main`) and `push` to `main`. Include `workflow_dispatch` so the user can re-run manually.
- Runner: **`windows-latest`**. The product has Windows-specific paths exercised by tests: `Cs2SetupTestCollection` (CS2 cfg path detection), `WindowsHotkeyService` registration, `WindowsProtectedSpotifySecretStore` (DPAPI). `ubuntu-latest` would either skip or fail those tests. Mirroring the product platform also makes the CI outcome match a tester machine. Document the choice in the workflow as a one-line comment on the `runs-on:` line.
- SDK setup: per UND-30 + UND-46 — pin a **single** `dotnet-version: 8.0.x` line in `actions/setup-dotnet@v4` (recommended: latest `8.0.x` available on the runner). Do **not** add a `9.0.x` line; the solution is uniformly `net8.0`.
- Steps: `actions/checkout@v4`, `actions/setup-dotnet@v4`, `dotnet restore`, `dotnet build --no-restore -c Release`, `dotnet test --no-build -c Release --logger "trx" --results-directory TestResults`. Optionally upload `TestResults` as a workflow artifact for debugging — that is not "user-facing distribution" and is allowed.
- NuGet caching: enable via `actions/setup-dotnet@v4`'s `cache: true` if it is straightforward. If caching introduces non-trivial maintenance, leave it off and note the choice in the workflow.
- Failure: any non-zero exit from build or test must fail the workflow (default behaviour; do not add `continue-on-error`).
- **No artifact publishing for users.** No `dotnet publish`, no GitHub Release, no NuGet push, no Docker build. UND-31's design controls when those land.
- **No secrets used.** This workflow must run on a fresh fork without any repository secrets configured.
- Add a CI status badge to the top of the repo `README.md`.
- Update `docs/backend-architecture.md` (or add a small `docs/ci.md` if more than a paragraph) with: where the workflow lives, what triggers it, the SDK setup it uses, and where to look for failed-test logs.

Deliverable:

- `.github/workflows/ci.yml`.
- README badge.
- Short docs note about CI.

Acceptance:

- Workflow runs on PR and `main` (verified by a test commit on a branch — but commit only on user go-ahead, and only on `main` per the working agreement, so the first verification is the merge to `main` itself).
- `dotnet restore` / `dotnet build` / `dotnet test` all run.
- Build or test failures fail the check.
- SDK setup is single-pinned to `8.0.x` per `docs/dotnet-tfm-decision.md` + UND-46. No `9.0.x` channel installed. Cite the doc + commit `09c245d` in the workflow YAML comment or the commit message.
- No artifacts are published to users.
- No repository secrets are required for the workflow to run.
- README has a CI badge linking to the workflow.
- Docs note exists and points at the workflow.
- All 170 tests run on the runner and pass, including `GsiHost.Tests/CredentialAndTokenLoggingRedactionTests` which gates the no-secrets-in-logs invariant. The CI matches the local count exactly (no skipped tests).

When done: present the workflow + diff to the user. After approval, commit to `main`, push, watch the first run on `main` complete green, then move UND-33 to `Done` with a comment linking the commit SHA and the green run URL. If the first run fails, fix forward in the same Linear issue (no amend after push; new commits) until green.

## Definition of done for this hand-off

You are done when:

- UND-33 is `Done` in Linear with a commit on `main` and a green workflow run for the head commit.
- A second-time contributor can clone the repo, push to a fork branch, open a PR, and watch the same workflow run automatically without configuring any secrets.
- The workflow respects the artifact-policy boundary from UND-31 (today: nothing user-facing published).
- All hard invariants above are still true; CS2 baseline tests stay green; UND-37 territory is untouched.

## Start here

1. Confirm UND-30 (hand-off #1) is `Done` (it should be — research doc in `515b42f` + UND-46 implementation `09c245d`). Confirm UND-31 (hand-off #2) status: if `Done`, follow its artifact-policy boundary; if still `In Progress`, ask the user whether to ship a conservative default (single pinned SDK, no artifact publishing) and revise later.
2. Read all docs under "Required reading".
3. Open UND-33; move to `In Progress`; post a brief plan comment.
4. Write the workflow locally; run `dotnet build` + `dotnet test` to confirm parity with the workflow steps; show the diff to the user.
5. On go-ahead: commit to `main`, push, watch the first run, fix forward if needed until green, then move UND-33 to `Done` with a comment linking the commit SHA and the green run URL.

If anything blocks you (missing AC, contradiction with workspace rules, hard-invariant risk, drift into UND-37 territory, scope creep into publishing/signing), stop and ask. Do not silently expand scope.
