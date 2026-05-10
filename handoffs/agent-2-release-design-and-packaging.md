# Hand-off #2 — Release pipeline design + Windows packaging + tester checklist (UND-31 + UND-32 + UND-35)

You are taking over the **release & delivery design** for Undefault. Three sequential issues. Mostly research and documentation; one issue may include a small `dotnet publish` prototype but no CI publishing.

This work is **disjoint from the multi-game scenarios axis (UND-37 and its sub-issues)** — that track is being driven by other agents. Do not touch UND-37 territory (see "Out of bounds").

You depend on hand-off pack #1 (UND-34 specifically) for the secret-handling policy that this pack will reference. UND-30 and UND-34 are `Done` (research docs landed in `515b42f`); the OAuth model is also **implemented and shipped** in `9407ab7` (UND-47, PKCE) and refined in `d9f5de9` (UND-49, dead-code + doc sweep). UND-48 (Spotify Developer app registrations on the Spotify Dashboard) is `Backlog`/blocked-on-user — it gates the actual tester-drop launch but **does not block** this pack's design work. Proceed.

## Product framing (do not violate)

- Undefault is a **local Spotify playback controller** that reacts to game state. Not a synchronized soundtrack, not a way around Spotify rules.
- CS2 is the first title; Dota and others come later (UND-37 track).
- **Safety dominates adaptivity.** `MusicSafetyState.Danger` overrides any positive playback intent.
- **Exactly one Spotify side-effect path per `/gsi` tick** (existing invariant).
- `Hotkeys + Timeline` is **tester / product-owner-only** intent-capture tooling, gated by `RuntimeOptions.Mode = intent_capture`.
- **No secrets in repo, artifacts, or logs.** Includes Spotify refresh tokens, access tokens, and the encrypted store path's contents. Post-PKCE (UND-47) Undefault no longer reads, prompts for, or persists a Spotify `client_secret` at all; if a future change tries to reintroduce one, that is a release blocker.

## State of the world (already on `main`)

- 170 tests green: Core 64, GsiHost 62, Cs2Simulator 44.
- Default product flow today: `GsiHost` boots a Minimal API, `ConsoleLaunchBootstrap` resolves the Spotify `CLIENT_ID` (env → encrypted Windows store → `appsettings.json` → interactive prompt), `Cs2SetupService` writes the CS2 GSI cfg automatically. The OAuth flow is Authorization Code with PKCE; no `client_secret` is read or persisted.
- TFM: solution is uniformly on `net8.0` after UND-46 (commit `09c245d`). UND-30's report (`docs/dotnet-tfm-decision.md`) is the rationale; this pack only references it.
- Local config files that are environment-specific and must **not** be shipped pre-populated in a tester artifact: any encrypted secret store output, any `profiles.json` with personal Spotify URIs, any local logs. Note: `Spotify.ClientSecret` is no longer a JSON key in any shipped config (UND-47 removed it from `GsiHost/appsettings.json` outright; `AppSettingsConfigurationService` actively strips it on save).

## Working agreement

- **Linear is the source of truth.** `plugin-linear-linear` MCP tools. Workspace `undefault`, team `Undefault`. AC wins over description.
- **No PRs, no feature branches.** Commit straight to `main` only after explicit user go-ahead.
- **Status discipline.** Issue `Backlog` → `Todo` → `In Progress` before starting; brief plan comment; `Done` only after user approval; on Done, drop a one-comment summary with doc path and commit SHA.
- **Comments policy in code (UND-32 prototype only).** Only when they explain non-obvious intent, trade-offs, or safety invariants. No XML doc that restates the symbol name. No "Phase X / UND-NN" narration in code. No TODO without a Linear ID.
- **Tests first-class (UND-32 prototype only).** If UND-32 lands a `dotnet publish` script or similar, run `dotnet build` and `dotnet test` before committing.
- **No silent follow-up issues.** Suggest implementation issues in writing; ask the user before creating them.

## Hard invariants you must not put at risk

1. CS2 baseline behaviour is byte-for-byte identical: `POST /gsi` round_start ducks, death restores, all 170 tests stay green (Core 64, GsiHost 62, Cs2Simulator 44).
2. Manual intent (`POST /user-actions`, Windows hotkeys) keeps using `ISpotifyPlaybackControl` directly, does not flow through `RulesEngine.ActionMap`.
3. `intent_capture` runtime mode stays gated: tester endpoints (`/timeline`, `/timeline/episodes`, `/user-actions`) return 404 in `scenario_playback`; the Windows hotkey hosted service does not start in `scenario_playback`; manual event keys still require the `custom:*` namespace.
4. **No secrets in repo, artifacts, or logs.** Whatever this pack produces — design, packaging recipe, checklist — must defend this. Specifically: no `Spotify.ClientSecret` JSON key in any shipped `appsettings.json`, no token-shaped string in shipped logs, no `client_secret` env var dropped into a publish step. `GsiHost.Tests/CredentialAndTokenLoggingRedactionTests` enforces the log-side rule today; the artifact-side rule is enforced by UND-35's grep checklist.

## Required reading

- `docs/README.md` — doc index.
- `docs/backend-architecture.md` — host topology, console bootstrap, persistence services, runtime modes, HTTP surface.
- `docs/spotify-developer-compliance-notes.md` and `docs/spotify-playback-policy-boundary.md` — Spotify rules; do not build a synchronized soundtrack.
- `docs/quick-launch.md` — mock/skip-based startup; useful for thinking about smoke tests.
- `docs/manual-intent-timeline.md` — manual intent / timeline capture; relevant for the tester smoke checklist.
- `docs/failure-safety-spec.md` — degraded-state behaviour you reference in the checklist.
- The output docs of hand-off pack #1, all already on `main`: `docs/dotnet-tfm-decision.md` (UND-30, commit `515b42f`) and `docs/spotify-oauth-secrets-model.md` (UND-34, commit `515b42f`). The OAuth model is **implemented** in `9407ab7` (UND-47 PKCE) and `d9f5de9` (UND-49 cleanup). **These are required inputs** for this pack — design must match what already shipped, not what UND-34 originally proposed in the abstract.
- `GsiHost/Services/ConsoleLaunchBootstrap.cs` — what credentials and flags exist today.
- `GsiHost/Services/Cs2SetupService.cs` — Windows path detection / cfg installation pattern (reference for packaging UX).
- `GsiHost/appsettings.json`, `GsiHost/control-profiles.json`, `GsiHost/smart-track-starts.json` — what may ship inside an artifact vs what is generated locally on first run.

## Out of bounds (do not touch — UND-37 territory)

- `Core/Adapters/*`, `Core/Music/*`, `Core/Rules/*`, `GsiHost/Adapters/Cs2GameAdapter.cs`, `GsiHost/Services/GsiProcessingService.cs`, `docs/multi-adapter-routing.md`, `docs/rules-engine-migration.md`, `docs/neutral-signals-and-game-clock.md`, `docs/ingestion-spec-cs2-dota.md`, `docs/volume-composition-spec.md`. Read freely; modify only with explicit user permission.
- Open Linear issues under UND-37: UND-40, UND-41, UND-44, UND-45, UND-43, UND-42, UND-14, UND-18, UND-5, UND-15, UND-16, UND-17.

## Hand-off pack #1 follow-ups (already shipped or in flight; reference, do not re-decide)

- UND-30 / UND-34 — research docs (Done, commit `515b42f`).
- UND-46 — TFM align on `net8.0` (Done, commit `09c245d`).
- UND-47 — Spotify OAuth PKCE switch + log redaction tests (Done, commit `9407ab7`).
- UND-48 — Spotify Developer app registrations (Backlog, **operational**, blocked on the user creating `undefault-dev` and `undefault-test` on the Spotify Developer Dashboard). Your tester-drop launch depends on `undefault-test` being registered with redirect URI `http://127.0.0.1:5292/callback` and the project's tester accounts added to its Development-Mode user list.
- UND-49 — post-PKCE dead-code + clean-file doc sweep (Done, commit `d9f5de9`).
- UND-50 — README + `docs/backend-architecture.md` post-PKCE sweep (Backlog, blocked-by UND-37).

## Issue order — sequential

### 1. UND-31 — Release pipeline design (CI/CD + delivery)

Linear: <https://linear.app/undefault/issue/UND-31>

Design doc, no code.

Scope:

- Pick the artifact model for the first internal-tester drop and beyond: framework-dependent vs self-contained, portable zip vs single-file exe vs installer (MSIX / Inno Setup / WiX). Justify the choice for the **MVP tester drop**, and note what changes when the channel goes wider.
- Delivery channels: GitHub Actions artifacts vs GitHub Releases (private/draft) vs another store. Versioning scheme: semver + commit SHA + build number.
- Update story: explicitly say whether auto-update is in MVP or out (default: out — manual swap is fine for first testers).
- Windows UX concerns: SmartScreen reputation, firewall prompt for the Kestrel localhost binding, install location (per-user vs Program Files), where logs land, code-signing timing (when do we need a real cert vs when can we ship unsigned with a documented SmartScreen workaround).
- Rollback: how a tester returns to the previous build, where prior builds live.
- Pull the secret-handling policy directly from `docs/spotify-oauth-secrets-model.md`; UND-47 has already shipped PKCE, so the policy is **enforced in code today**, not aspirational. The design must not contradict the doc's §"Security checklist" or weaken the existing `CredentialAndTokenLoggingRedactionTests` guarantees.
- Pull the SDK policy from `docs/dotnet-tfm-decision.md` — solution is on `net8.0` (LTS); CI pins the `8.0.x` channel and does **not** install `9.0.x`.
- Surface UND-48 (Spotify Developer app registrations) as a hard prerequisite for the actual first tester-drop launch: the artifact must ship the `undefault-test` `client_id`, which only exists after that registration is created on the Spotify Dashboard. UND-48 does not block this design issue, but it does block the `Done` of the implementation issue that ships a real artifact.

Deliverable:

- New doc at `docs/release-pipeline-design.md`.
- A list of follow-up implementation issues **suggested but not created** until the user approves. Likely candidates: GitHub Actions release workflow, code-signing setup, GitHub Release publishing, auto-update spike (if MVP), versioning automation.

Acceptance:

- Artifact model is named with a one-paragraph rationale.
- Delivery channel for tester drop is named with rationale.
- Versioning scheme is written down.
- Auto-update decision is explicit (in or out for MVP).
- Windows UX risks are listed with mitigations.
- Code-signing timing is decided.
- Rollback plan is written.
- Secret-handling references UND-34's deliverable; SDK references UND-30's deliverable.

When done: present the doc to the user. After approval, commit to `main`, push, move UND-31 to `Done` with a comment linking the doc and commit SHA.

### 2. UND-32 — Windows client packaging research / prototype

Linear: <https://linear.app/undefault/issue/UND-32>

Research + small prototype only if it stays small.

Scope:

- Compare at least two practical packaging options in light of UND-31's chosen artifact model (e.g. portable zip with `dotnet publish --self-contained` vs single-file exe vs Inno Setup).
- Define the **artifact contents** explicitly: which projects' publish output ships (`GsiHost` is mandatory; simulator/diagnostics are optional and must be documented either way), which config files ship as templates (a sanitized `appsettings.json` whose `Spotify` section carries the `undefault-test` `client_id` only — no `ClientSecret` key per UND-47, no tokens; how that `client_id` is injected at publish time vs committed to the repo is the design decision), which configs the user generates locally on first run (encrypted secret store under `%LOCALAPPDATA%`, `profiles.json`, `smart-track-starts.json`), which README / `start.bat` ships in the zip.
- Where logs land for a tester install (per-user `%LOCALAPPDATA%` recommended) and how the tester grabs them for a bug report.
- Update story between builds: replace folder vs run installer; uninstall / cleanup steps.
- Code-signing: confirm UND-31's timing for first tester drop; do not introduce new signing requirements without ask.
- Optional small prototype: a `publish-tester.ps1` or top-level `Directory.Packaging.props` change (no installer tooling, no CI publishing). If the prototype grows past ~50 lines or touches `Program.cs`, **stop and ask** before continuing.

Deliverable:

- New doc at `docs/windows-client-packaging.md`.
- Optional prototype script at `scripts/publish-tester.ps1` (or similar). If you ship it, it must run locally on Windows and produce a self-contained zip into a known output folder; do not commit any output binaries.
- Suggested follow-up issues for full installer / code-signing pipeline — suggested only, not created.

Acceptance:

- Two packaging options are compared on artifact size, tester UX, update story, signing implications.
- Recommended option for the first tester drop is named.
- Artifact contents are enumerated explicitly (in / out, generated-on-first-run).
- Log location is decided.
- Update + uninstall story is documented.
- If a prototype script ships, it builds the artifact end-to-end on a clean checkout (without producing committed binaries) and `dotnet build` + `dotnet test` still succeed.

When done: present the doc (and prototype, if any) to the user. After approval, commit to `main`, push, move UND-32 to `Done` with a comment linking the doc and commit SHA.

### 3. UND-35 — Release checklist + smoke test before tester drop

Linear: <https://linear.app/undefault/issue/UND-35>

Process / docs only. Depends on UND-31 + UND-32 being `Done`.

Scope:

- Write a manual release checklist for tester builds:
  - Build version: commit SHA, build date, channel.
  - Launch on a clean Windows box, no dev tooling: app starts, console checklist appears, Kestrel binds the configured URL.
  - Spotify auth: PKCE login → token refresh → re-auth-after-restart (token storage is in-memory per `docs/spotify-oauth-secrets-model.md`; persistent storage is deferred). Confirm tokens never appear in logs (already covered by `GsiHost.Tests/CredentialAndTokenLoggingRedactionTests`, but verify on a clean Windows box too).
  - GSI sanity: hit `POST /gsi/reset`, post one synthetic `round_start` payload (or a CS2 simulator scenario), confirm `/diagnostics/music-shadow` records the shadow snapshot, confirm one duck call on `FakeSpotifyClient` equivalent path or via real Spotify with a test account.
  - Hotkeys + Timeline gating: confirm `/timeline`, `/timeline/episodes`, `/user-actions` return 404 in `scenario_playback` mode, and that they work in `intent_capture` mode.
  - Logs: path is discoverable (matches UND-32's decision); on the published artifact, a grep returns:
    - **zero hits** for `client_secret`, `ClientSecret`, `access_token`, `refresh_token`, and `Bearer\s+\S+` token-shaped strings.
    - **zero hits** for the JSON key `Spotify.ClientSecret` (no longer present in any shipped config per UND-47).
    - **at most one hit** for the configured `Spotify.ClientId` value, in the shipped `appsettings.json`. Document the expected `undefault-test` `client_id` value alongside the checklist so a tester can compare.
  - Rollback: how to swap back to the previous build.
  - Known issues: list anything explicitly limited in the build.
- Define what a tester must include in a bug report: version + commit SHA, Windows version, repro steps, log snippet, Spotify auth state, scenario / GSI capture if relevant.
- Reference UND-31's release pipeline and UND-32's packaging recipe rather than re-deriving them.

Deliverable:

- New doc at `docs/release-checklist.md`.
- No automation in this issue.

Acceptance:

- Checklist exists and is concrete enough to follow by hand without re-asking the team.
- Tester bug-report template is defined.
- Secret-leak check is part of the checklist explicitly.
- Hotkeys + Timeline gating is verified explicitly.
- Checklist matches UND-31 (artifact / channel) and UND-32 (publish recipe / log paths).

When done: present the doc to the user. After approval, commit to `main`, push, move UND-35 to `Done` with a comment linking the doc and commit SHA.

## Definition of done for this hand-off

You are done when:

- UND-31, UND-32, UND-35 are all `Done` in Linear with their docs on `main` and pushed.
- The team can pick up "build a tester drop end-to-end" using only `docs/release-pipeline-design.md`, `docs/windows-client-packaging.md`, and `docs/release-checklist.md` (and the optional `scripts/publish-tester.ps1` from UND-32).
- Hand-off pack #3 (CI implementation, UND-33) has a clear artifact-policy boundary to enforce: which artifacts the CI workflow may publish today (none, by default) vs which require future implementation issues.

## Start here

1. Confirm hand-off pack #1 status: UND-30, UND-34, UND-46, UND-47, UND-49 should be `Done`; UND-48 is `Backlog` (operational, blocked-on-user, gates the launch but not the design); UND-50 is `Backlog` (blocked-by UND-37). If anything diverges from this, stop and ask.
2. Read all docs under "Required reading".
3. Open UND-31; move to `In Progress`; post a brief plan comment.
4. Write the design; present to the user; on go-ahead, commit + push + Done with summary comment.
5. Repeat for UND-32, then UND-35.

If anything blocks you (missing AC, contradiction with workspace rules, hard-invariant risk, drift into UND-37 territory, prototype growing past the small-prototype boundary), stop and ask. Do not silently expand scope.
