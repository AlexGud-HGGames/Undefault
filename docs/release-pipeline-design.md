# Release Pipeline Design (UND-31)

Design / decision-only report. No CI workflow, no packaging script, no `*.csproj`
or `*.json` edits land from this issue. Implementation, when approved, is filed
as separately scoped Linear issues — see "Suggested follow-up implementation
issues" at the end. Hand-off pack #2's UND-32 (Windows packaging) and pack #3's
UND-33 (CI) consume this doc; they are not duplicated here.

## Scope

Decide the release contour for Undefault:

- which artifact shape we hand to internal testers,
- which channel delivers it,
- how we version it,
- whether MVP needs auto-update,
- how Windows-specific frictions (SmartScreen, firewall, install location, log
  location) are handled,
- when code-signing has to land,
- how a tester rolls back to a previous build,
- how every step above stays consistent with the secret-handling policy in
  `docs/spotify-oauth-secrets-model.md` and the SDK pin in
  `docs/dotnet-tfm-decision.md`.

Out of scope: Linux/macOS packaging (Undefault is Windows-only by today's
encrypted-secret-store contract — `ConsoleLaunchBootstrap.Apply` throws
`PlatformNotSupportedException` off Windows); marketing site / EULA / privacy
policy; UND-37 territory (`Core/Adapters/*`, `Core/Music/*`, `Core/Rules/*`,
multi-game scenarios).

## Inputs locked from prior work

These are not re-decided here; the rest of the doc is consistent with them.

- **`docs/dotnet-tfm-decision.md` (UND-30, Done in `515b42f` / aligned in
  `09c245d` via UND-46).** Solution is uniformly on `net8.0` (LTS). CI pins the
  `8.0.x` SDK channel (`actions/setup-dotnet@v4` `dotnet-version: 8.0.x`); no
  `9.0.x` SDK matrix. Tester-artifact runtime target is `net8.0`.
- **`docs/spotify-oauth-secrets-model.md` (UND-34, Done in `515b42f`;
  implemented by UND-47 in `9407ab7` and refined by UND-49 in `d9f5de9`).**
  Spotify OAuth is Authorization Code with PKCE. There is no `client_secret` in
  the client artifact. The 14-item §"Security checklist" is the contract this
  release pipeline must enforce.
- **UND-48 (Backlog, blocked-on-user, operational).** The `undefault-test`
  Spotify Developer registration must exist on the Spotify Developer Dashboard,
  with redirect URI `http://127.0.0.1:5292/callback` and tester accounts added
  to its Development-Mode user list, before the first real tester-drop launch.
  UND-48 does not block this design issue, but it does block the `Done` of any
  future implementation issue that ships a real artifact.
- **Test baseline must stay green.** Hand-off pack #2 cites 170 tests
  (Core 64, GsiHost 62, Cs2Simulator 44) as the pre-PKCE snapshot; UND-47
  added `GsiHost.Tests/CredentialAndTokenLoggingRedactionTests`, so the
  current count is at least one higher. The exact number is whatever
  `dotnet test` reports on `main`. No release step may regress that count
  or any individual test. Enforcing this is UND-33's `dotnet build` +
  `dotnet test` gate; this doc only states the requirement.

## Recommendation summary

| Decision | MVP tester drop | Notes for later |
|---|---|---|
| Artifact shape | Portable zip, **self-contained** `dotnet publish` of `GsiHost` for `win-x64`. No installer. | Re-evaluate once a non-developer audience exists — at that point an Inno Setup / WiX / MSIX installer becomes worthwhile. |
| Runtime | `net8.0` self-contained (the .NET 8 ASP.NET Core runtime ships inside the zip). | Migrate to `net10.0` per UND-30's follow-up criterion when LTS support window forces it. |
| Architecture | `win-x64` only. | `win-arm64` deferred until a tester needs it. |
| Delivery channel | GitHub Actions artifact from a `workflow_dispatch` build, downloaded by hand. | Promote to a GitHub **draft, private** Release once we hand a build to anyone outside the dev loop. |
| Versioning | `vMAJOR.MINOR.PATCH-tester.N+<git-sha7>` (SemVer 2.0 pre-release tag with build metadata). | Drop the `-tester.N` pre-release tag once we cut a stable release. |
| Auto-update | **Out of MVP.** Manual zip swap. | File a future spike issue if/when tester load justifies it. |
| Install location | Per-user. The tester unzips to wherever they like; `%LOCALAPPDATA%\Undefault\` is the documented recommendation. | Per-machine `Program Files` installs require an installer + admin rights — deferred. |
| Log location | `%LOCALAPPDATA%\Undefault\logs\` (proposed). Today logs are console-only; file logging is a deferred implementation issue. | Surface the path in the console checklist so testers can find it. |
| Code-signing | **Unsigned** for the first internal tester drop, with a documented SmartScreen workaround. | Code-signing certificate provisioning must happen before any wider tester audience or any GitHub Releases publish that's not invite-only. |
| Rollback | Tester keeps the previous zip; or re-downloads the prior CI artifact / draft Release. Manual. | No automated rollback in MVP. |

## Artifact model

### What gets published

Exactly one project gets published as a tester artifact: **`GsiHost`**. It
references `Core` and the simulator runtime libs, so those flow into the publish
output by transitive `dotnet publish` behavior. The simulator console
(`Cs2Simulator`) and test projects (`Core.Tests`, `GsiHost.Tests`,
`Cs2Simulator.Tests`) are **not** part of the tester artifact. Reasons:

- Tests are not a deliverable. Shipping test binaries widens the surface area
  scanned by SmartScreen / antivirus and grows the zip with no tester benefit.
- The simulator console is a developer tool. A tester drop that includes it
  invites confusion ("which exe do I run?"). Future tester scenarios that need
  scripted CS2 input can ship the simulator separately, or as an opt-in extra
  zip — that's a UND-32 conversation, not a UND-31 one.

UND-32 enumerates the exact contents of the zip (config templates, README,
`start.bat`, etc.). This doc only nails the boundary: `GsiHost`'s publish output
is mandatory; everything else is documented either way.

### Self-contained vs framework-dependent

| Aspect | Self-contained (`--self-contained true`) | Framework-dependent (`--self-contained false`) |
|---|---|---|
| Tester install steps | Unzip → run. No prerequisite. | Unzip → install ".NET 8 Desktop / ASP.NET Core 8 Runtime" → run. Two steps, one of them a separate Microsoft installer. |
| Artifact size (rough order of magnitude) | ~100 MB unpacked / tens of MB compressed (the .NET 8 ASP.NET Core runtime ships inside). Exact size depends on trimming choices, which UND-32 owns. | ~5–10 MB unpacked / a few MB compressed. |
| Update flow | Replace the whole folder per build. | Replace the folder; runtime stays. |
| Failure mode if tester forgets a step | None — it just runs. | App fails to start with a runtime-missing error. Tester friction multiplied. |
| Patch surface | Each artifact carries its own runtime; a Microsoft .NET 8 servicing patch is picked up only on the next build. | Tester's installed runtime gets serviced by Microsoft Update independently of our build cadence. |
| Build complexity | Standard `dotnet publish -r win-x64 --self-contained true`. | Standard `dotnet publish -r win-x64 --self-contained false`. |

**Pick: self-contained.** The MVP audience is a small invite-only tester pool
of people we want to spend zero patience on a Microsoft runtime installer.
Trading the larger artifact size for "unzip → run" is worth it for the first
drops.
A tester does not benefit from our app picking up a Microsoft .NET 8 servicing
release out-of-band; we'd rather have the build SHA we shipped match the
runtime they're running. The framework-dependent option is documented above
because it becomes the better choice when we move to an installer (the
installer can declare a runtime prerequisite and Windows will fetch it), but
that's a UND-32 / future decision, not the MVP.

UND-32 will compare the two within self-contained land — single-file exe vs
unpacked folder vs Inno Setup wrapper. This doc does not preempt that.

### Single-file vs folder layout

Not pre-decided here; UND-32 owns the call. Both fit inside the
"self-contained portable zip" envelope and neither changes the channel,
versioning, or signing decisions below.

### Installer formats considered for MVP

Considered and **rejected for the MVP tester drop**:

- **MSIX** — needs an entry on the Microsoft Store partner program or sideload
  trust establishment per machine, and a code-signing cert. SmartScreen-friendly
  but heavy for a one-tester audience. Filed as a follow-up if and when we go
  wider.
- **Inno Setup / WiX** — produces a real installer, can drop a Start menu
  shortcut, and can declare runtime prerequisites. Useful when we want a
  per-machine install or a guided UX, but for the MVP every advantage they
  bring (start menu entry, prereq fetch) is dominated by "tester unzips a
  folder; less to misconfigure." Filed as a follow-up.
- **MSI / EXE bootstrapper** — same reasoning as Inno Setup; useful later, not
  needed now.

The MVP zip layout (sanitized `appsettings.json` carrying the `undefault-test`
`client_id`, README, optional `start.bat`, encrypted secret-store file
generated on first run under `%LOCALAPPDATA%\Undefault\`) is UND-32's contract.

## Delivery channel

### MVP: GitHub Actions workflow artifact

The MVP tester drop is built by a future GitHub Actions publish workflow
(separately filed implementation issue, not part of UND-33's first cut). The
trigger shape is **manual `workflow_dispatch` against a ref pointing at a
release tag**, with the workflow asserting the ref is a tag matching the
versioning scheme below before publishing. (`push: tags:` is an equivalent
trigger if we'd rather publish automatically on every tag push; `workflow_dispatch`
keeps a human in the loop.) The published artifact is a GitHub Actions
**artifact** (`actions/upload-artifact@v4`) attached to the workflow run,
downloaded via the run page UI or `gh run download`.

Why this is enough for the first internal-tester drop:

- The audience is small and authenticated — every tester has a GitHub account
  with at least read access to the repo. Anyone who shouldn't see the build
  can't see it.
- Zero auth / distribution infra to build out.
- Retention is bounded by GitHub's artifact-retention setting (default 90
  days; configurable per repo). Bounded retention is fine for tester
  iteration — it forces "this is not the canonical release" framing — and is
  one of the reasons the next step up is a Release, not a longer-retention
  artifact.
- Every artifact is bound to a workflow run, which is bound to a commit SHA
  and the workflow's logs. Reproducibility is implicit.

CI **must not** publish an artifact today — UND-33's first cut is `restore` /
`build` / `test` only, and UND-33's design must record that the user-facing
publish step requires a separately-filed implementation issue. See the
"Artifact-policy boundary for hand-off pack #3" section below.

### Once we hand a build to a non-developer: GitHub Releases (draft, private)

When the tester audience grows past developer-with-repo-access — typically the
first non-developer tester or any tester we don't want to grant repo access to
— promote the same artifact to a **GitHub draft Release** in this private
repository. Drafts are visible only to repo collaborators; the asset URL is
not enumerable to the public. Release assets do **not** expire on GitHub's
artifact-retention timer, which is a second reason to promote: build artifacts
disappear after retention, release assets stay attached to the tag.

The release tag follows the versioning scheme below.

Why not GitHub Releases (public) for the MVP: this repo is private and the
tester program is invite-only. Public releases come later, and only after
code-signing lands — see "Code-signing timing" below.

### Channels we explicitly are not using for MVP

- **Microsoft Store / MSIX channel.** Requires a partner program account,
  publisher identity, and ongoing certification. Worth pursuing if and when we
  move to a public audience.
- **Auto-update server / Squirrel / Velopack / Sparkle.** Auto-update is out
  of MVP (see below); the server they need is out of scope.
- **Direct download from a marketing site.** No marketing site exists, and
  building one is out of scope here. If this ever becomes the public channel,
  it goes through GitHub Releases (public) backed by code-signed binaries —
  not a separately-managed CDN, until traffic justifies it.

## Versioning scheme

Format:

```text
v<MAJOR>.<MINOR>.<PATCH>-tester.<N>+<git-sha7>
```

Concrete example: `v0.1.0-tester.3+abc1234`.

- **`MAJOR.MINOR.PATCH`** — SemVer 2.0. Pre-1.0 while the tester program is
  invite-only and the public surface (config schema, HTTP routes, scenario
  contract) is still mutable. The very first tester build is `v0.1.0`.
- **`-tester.<N>`** — SemVer pre-release identifier. Marks the build as
  invite-only. Increments per build within a `MAJOR.MINOR.PATCH` line. Drops
  off when we cut a non-tester release; until then, every artifact carries it.
- **`+<git-sha7>`** — SemVer build metadata. The 7-character commit SHA the
  workflow built from. Build metadata is informational; SemVer comparators
  ignore it, which is the behavior we want (two artifacts at the same
  pre-release version that differ only in metadata are the same product
  version conceptually).

What the version is bound to:

- **Tag.** The git tag triggers (or is referenced by) the `workflow_dispatch`
  build. Tag name == release name == artifact directory prefix.
- **Assembly metadata.** `<Version>` (or equivalent, e.g. via `MinVer` /
  `Nerdbank.GitVersioning` / a CI-injected `<Version>` MSBuild property) lands
  in `GsiHost.dll`'s assembly info so the running host can print it on the
  console checklist and a tester bug report can carry it. The exact mechanism
  is UND-32's call (whichever fits the publish flow); from this design's
  point of view the version string just has to round-trip from the tag to
  `Assembly.GetName().Version` / `AssemblyInformationalVersion`.
- **Console checklist (proposed; not implemented today).** The current
  startup checklist printed by `GsiHost` (Spotify credential readiness,
  redirect URI, GSI URL, CS2 cfg state, control-profile file, Smart Track
  Start status, Spotify auth status — see
  `docs/backend-architecture.md` §"Console Checklist") does **not** include a
  build version line. Adding one is part of the versioning-automation
  follow-up issue listed at the end of this doc; UND-35's release checklist
  needs that line in place to do its job.

What versioning does **not** do:

- It does not gate Spotify behavior. Scopes, redirect URI rules, and the OAuth
  flow do not change between tester builds; if they ever do, that becomes a
  hard-versioned migration with its own checklist, not a silent config flip.
- It does not encode the scenario pack version. Scenario data lives in
  separately-versioned config files (UND-37's track), not in the host
  artifact's SemVer.

## Auto-update decision

**Out of MVP.** The first tester drops do not include any auto-update
mechanism. A new build = manual zip swap by the tester.

Reasons:

1. The tester audience is small enough that "send a link in the tester chat,
   re-download" is faster to set up than any auto-updater.
2. Every credible auto-update path (Velopack / Squirrel / Sparkle / a
   homegrown delta server) needs a hosted manifest, an asymmetric trust root
   (signing key), and an unattended-update story for tokens. The trust root
   means we need code-signing first; the unattended-update story means the
   updater inherits the secret-handling rules in
   `docs/spotify-oauth-secrets-model.md` §"Security checklist" item 14. None
   of this fits the MVP.
3. An auto-updater that runs in the background changes the SmartScreen and
   firewall surface — it is itself a long-lived process, possibly with elevated
   permissions, that we'd be defending. Out of scope here.

The auto-update spike is recorded as a suggested follow-up issue at the end of
this doc.

## Windows UX risks and mitigations

### SmartScreen (unsigned binaries)

Windows Defender SmartScreen flags unsigned executables on first launch with a
"Windows protected your PC" dialog. The user has to click *More info* →
*Run anyway*. Without code-signing, every tester sees this on every fresh
install of every new version.

**MVP mitigation:** ship unsigned, with the workaround documented in the
tester README and reinforced in the UND-35 release checklist. The README must
explicitly tell the tester:

- the dialog is expected,
- the steps to bypass it (*More info* → *Run anyway*),
- to verify the SHA-256 of the downloaded zip against the SHA published in
  the GitHub release / artifact summary before bypassing SmartScreen.

This mitigation is acceptable for an invite-only tester pool. It is **not**
acceptable for a public release; see "Code-signing timing".

### Code-signing timing

| Audience | Signing requirement |
|---|---|
| Internal-tester drop (invite-only, GitHub Actions artifact, ≤ ~10 testers in a chat) | Unsigned acceptable, with the SmartScreen-bypass workaround in the tester README. |
| First non-developer tester / first GitHub draft Release handed to anyone outside the dev loop | Code-signing **required**. EV (hardware-backed) cert preferred — it shortens the SmartScreen reputation-accumulation window relative to OV/IV but does not eliminate it; SmartScreen reputation is still earned through usage even for EV-signed binaries post-2022. Standard OV/IV cert acceptable as a fallback if EV provisioning blocks the timeline. |
| Any public Release | Code-signed. EV preferred for the faster reputation ramp. |

The cert provisioning step is filed as a suggested follow-up issue. The cert
itself is a secret per `docs/spotify-oauth-secrets-model.md` §"CI / local /
artifact secret split"; it lives in GitHub Actions secrets
(`WINDOWS_CODE_SIGN_CERT`, `WINDOWS_CODE_SIGN_PASSWORD`) once the
implementation issue lands, never in the repo.

### Firewall (Windows Defender Firewall)

`SpotifyOAuthService.GetRedirectUri()` enforces a loopback IP literal
(`127.0.0.1`, not `localhost`); `ConsoleLaunchBootstrap.NormalizeBaseUrl`
re-normalizes any configured `Gsi:Url` to the loopback host. Loopback binds
do not trigger the public-network firewall prompt on Windows by default.

**Mitigation:** UND-35's release checklist verifies on a clean Windows box
that no firewall prompt appears on first launch. If a prompt ever appears,
it means the artifact was misconfigured to bind `0.0.0.0` and the build is
rejected. (Today this would also break Spotify OAuth, which rejects non-
loopback redirect URIs, so the bug would surface in the smoke test
regardless.) This is consistent with `docs/spotify-oauth-secrets-model.md`
§"Redirect URI strategy" → "Windows constraints to surface".

### Port collision

Default bind is `127.0.0.1:5292`. If the port is in use Kestrel fails to
start. The tester README and the UND-35 checklist must include a port-busy
recovery step, configurable via `Gsi:Url` in the shipped `appsettings.json`.
The redirect URI is renormalized off the same `Gsi:Url` host:port, so
overriding the port is a single-line config edit.

If a tester picks a non-default port, they must register the matching
`http://127.0.0.1:<port>/callback` URI on their Spotify Developer app
(`undefault-test` or their own `undefault-dev`), since Spotify's redirect
URI list is exact-match. UND-35 documents this; UND-31 does not pre-register
arbitrary ports on the Spotify side.

### Install location

**Per-user, in the directory the tester picks at unzip.** Recommended path
documented in the tester README: `%LOCALAPPDATA%\Undefault\`. Reasons:

- Per-user installs do not require admin rights, which keeps SmartScreen
  surface and UAC prompts off the table for the MVP.
- The encrypted secret store (`WindowsProtectedSpotifySecretStore`, DPAPI
  `CurrentUser`) already writes under `%LOCALAPPDATA%`; co-locating the app
  with its state is the lowest-friction layout.
- A per-machine `Program Files\Undefault\` install requires an installer +
  admin elevation. Filed as a follow-up issue alongside the installer pick.

The installer-based per-machine layout, when it lands, must keep the
encrypted secret store **per-user** (DPAPI `CurrentUser` cannot be otherwise
without weakening at-rest protection — see
`docs/spotify-oauth-secrets-model.md` §"Security checklist" item 6).

### Log location

**Recommended (proposed):** `%LOCALAPPDATA%\Undefault\logs\`.

Today, `GsiHost` uses default ASP.NET Core console logging only — no file sink
is registered. A tester reproducing a bug copy-pastes the console window
output. That's enough for the very first internal drops, but the path above
is the recommended target so that:

- when a file sink is added, it lands in a per-user, non-admin path that the
  tester can find without spelunking;
- the bug-report template (UND-35) can give an exact path to attach;
- the secret-redaction tests (`GsiHost.Tests/CredentialAndTokenLoggingRedactionTests`)
  apply to the file output by construction (they exercise the same logger
  pipeline).

Adding the file sink is a suggested follow-up implementation issue. UND-32
confirms the exact path under `%LOCALAPPDATA%\Undefault\` once chosen.

### Antivirus false positives

Any unsigned binary can trip heuristic AV flagging on Windows; self-contained
.NET publishes are not special here, but they ship a larger, more
"installer-shaped" payload than a framework-dependent publish, so a flag is
slightly more likely. Code-signing is the primary mitigation — signed
binaries reduce hits across most AV products. For the MVP this is documented
as a known risk in the tester README; if a tester is hard-blocked, the
fallback is a framework-dependent build of the same commit (filed as a
follow-up only if the issue actually surfaces — not built speculatively).
Maintaining two publish profiles is a real cost; we eat that cost only if
testers report the problem.

## Rollback plan

**Manual, two paths.**

1. **Tester's local copy.** The tester README instructs testers to keep the
   previous zip in a sibling folder (`Undefault-v0.1.0-tester.2/` next to
   `Undefault-v0.1.0-tester.3/`). Reverting is "stop the host, rename the
   active folder, point at the previous one." This is the primary rollback
   path for the MVP.
2. **Re-download.** If the tester nuked the previous build, the prior GitHub
   Actions artifact (within retention, ~90 days) or the prior GitHub draft
   Release asset is re-downloadable. Tester re-extracts; same as a fresh
   install.

The encrypted secret store and any user-generated config files
(`profiles.json`, `smart-track-starts.json`) live under `%LOCALAPPDATA%` and
are **not** part of the artifact. They survive a rollback, which is the
behavior we want — the tester does not need to re-enter their Spotify
`client_id` after a version swap. The reverse: if a build is rolled back
*because* of a config schema break, the tester runs `--reset-spotify-secrets`
or `--clear-spotify-secrets` to wipe the cached state and re-prompt. The flag
inventory matches what `ConsoleLaunchBootstrap` already implements.

What rollback does **not** cover in MVP:

- No automated rollback on auto-update failure (auto-update is out of MVP).
- No telemetry-driven rollback decision. Rollback is a human call, made in
  the tester chat.
- No data migration story between forward/back versions. Config files are
  expected to stay backward-compatible across tester builds; if a build needs
  a hard schema bump, the build's release notes (and the UND-35 checklist
  entry for that build) must explicitly say "rollback requires
  `--clear-spotify-secrets`."

## Secret-handling — pulled through from UND-34

The secret-handling rules below are **the same** rules as
`docs/spotify-oauth-secrets-model.md` §"CI / local / artifact secret split"
and §"Security checklist", restated here only as the contract for the
release pipeline. If anything below appears to disagree with that doc, the
doc wins.

### What the artifact may contain

- The `undefault-test` `client_id` value, embedded in the shipped
  `appsettings.json`'s `Spotify.ClientId` field. `client_id` is not a secret in
  the cryptographic sense; Spotify treats it as a public identifier
  (UND-34 §"Spotify Developer app registrations").
- A sanitized `appsettings.json` whose `Spotify` section carries **no**
  `ClientSecret` key (post-UND-47 the key is removed outright; the artifact
  must not reintroduce it).
- A sanitized `control-profiles.json` whose contents contain no Spotify URIs
  tied to the developer's personal account. UND-32 owns the exact contents
  of any default control-profile shipped.

### What the artifact must never contain

- `client_secret` — for any registration. PKCE means there is no secret to
  ship.
- `access_token`, `refresh_token` — token storage is in-memory per
  `docs/spotify-oauth-secrets-model.md` §"Token storage recommendation"; the
  artifact ships zero tokens.
- The encrypted secret-store file — generated on first run under
  `%LOCALAPPDATA%\Undefault\`, never bundled.
- Any `profiles.json` containing personal Spotify URIs.
- Build / debug logs from the build machine.
- A `.git/` directory, `.env*` files, contributor-local
  `appsettings.Development.json`, contributor-local secrets in any form.

### What the build pipeline must enforce

The build workflow (UND-33's territory; UND-31's contract) must:

1. Inject the `undefault-test` `client_id` into the published
   `appsettings.json` either at publish time from a GitHub Actions secret
   (`SPOTIFY_TEST_CLIENT_ID`) or by committing a known-public value to a
   release-only config template — UND-32 picks. Either way, the value lands
   in exactly one place in the shipped zip.
2. Run a "leak-grep" step on the published output before producing the zip.
   The grep matches the same patterns UND-35's manual checklist runs:
   - **zero hits** for `client_secret`, `ClientSecret`, `access_token`,
     `refresh_token`, and `Bearer\s+\S+` token-shaped strings.
   - **zero hits** for the JSON key `Spotify.ClientSecret`.
   - **at most one hit** for the configured `Spotify.ClientId` value (in
     `appsettings.json`).
   Failure is a build failure, not a warning. This is the artifact-side
   counterpart to `GsiHost.Tests/CredentialAndTokenLoggingRedactionTests`'s
   log-side enforcement and is required by UND-34 §"Security checklist"
   item 13.
3. Never run real OAuth in CI (UND-34 §"Security checklist" item 8). Tests
   continue to use `MockSpotifyClient` / `InMemoryTokenStorage` /
   `IHttpClientFactory` fakes.
4. Never write a `client_secret` to disk in any build step. The PKCE switch
   means there is no operational need for one in CI; if an agent ever adds
   one back, that's a release blocker.

### Where each secret lives

This table is a strict subset of UND-34's table, restated for the release
pipeline:

| Value | Repo | Tester artifact | CI (GitHub Actions) | Build logs |
|---|---|---|---|---|
| `client_id` (`undefault-test`) | Optional, by policy not committed; injected at publish time. | Yes — in shipped `appsettings.json`. | GitHub Actions secret `SPOTIFY_TEST_CLIENT_ID`. | Never echoed. |
| `client_secret` (any) | **Never.** | **Never.** | **Never.** | **Never.** |
| `access_token`, `refresh_token` | Never. | Never. | Never. | **Never.** |
| Code-signing cert (when added) | Never. | Never (only the resulting signed binary). | GitHub Actions secrets `WINDOWS_CODE_SIGN_CERT`, `WINDOWS_CODE_SIGN_PASSWORD`. | Never. |

### Auto-update interaction with secret model

If/when auto-update lands, it must not deliver `client_id` overrides at
runtime, must not introduce a new server-side identity (the manifest server
is not an SDA, it's a static asset host), and must not reintroduce a
`client_secret` to support its own auth flow. This row is UND-34 §"Security
checklist" item 14 verbatim and is restated here only because the auto-update
spike will read this doc first.

## Artifact-policy boundary for hand-off pack #3 (UND-33)

This is the contract UND-33's CI workflow must encode and UND-33's design
doc must restate.

- **CI today:** `actions/checkout` → `actions/setup-dotnet@v4` with
  `dotnet-version: 8.0.x` → `dotnet restore` → `dotnet build` → `dotnet test`.
  No publish step. No upload of any user-facing artifact. The only artifact
  CI may publish today is `TestResults` (or equivalent test-output blob)
  attached to the workflow run for debugging — never a runnable zip.
- **CI later:** the tester-drop publish workflow is a **separately filed
  implementation issue**, not part of UND-33's first cut. That issue carries
  its own AC, including the leak-grep step, the `SPOTIFY_TEST_CLIENT_ID`
  injection step, the artifact upload, and the GitHub Releases (draft)
  promotion path. UND-33's design doc records this boundary explicitly so an
  agent picking up UND-33 does not silently extend scope.
- **Triggers:** `pull_request` and `push` to `main` for the
  build/test workflow. The publish workflow, when it lands, is
  `workflow_dispatch` (manual) with the workflow asserting the dispatched
  ref points at a tag matching the versioning scheme above; `push: tags:`
  is an acceptable equivalent if the implementer prefers automatic publish
  on tag push.
- **SDK pin:** `8.0.x` channel only. Do not install `9.0.x` (UND-30
  §"Implications for downstream issues").

## Cross-checks (each one a hard requirement)

The doc must not contradict any of these. If a contradiction surfaces during
implementation, this doc is wrong and gets revised before the implementation
ships.

1. `docs/spotify-developer-compliance-notes.md` — the release pipeline does
   not introduce any synchronization-of-recordings, mix/segue/overlap, game,
   voice, ringtone, analytics, or AI-training behavior. This is mostly
   trivially satisfied (the pipeline produces a zip; it does not author any
   product behavior), but is restated for the record.
2. `docs/spotify-oauth-secrets-model.md` §"Security checklist" — items 1–14.
   Items 1, 2, 3, 4, 8, 13 are the items the release pipeline can affect;
   the rest are out-of-band invariants the pipeline must not weaken (e.g.
   loopback redirect URI, encrypted-store-on-Windows-only, scopes minimal).
3. `docs/dotnet-tfm-decision.md` — `8.0.x` SDK pin; `net8.0` runtime in the
   self-contained publish.
4. `docs/spotify-playback-policy-boundary.md` — local playback control, not a
   synchronized soundtrack. Not at risk from packaging decisions; restated
   for completeness.
5. `docs/failure-safety-spec.md` — degraded-state behavior is not changed by
   how the artifact is packaged; the conservative defaults (Unknown / Danger)
   keep applying. The release checklist (UND-35) verifies this on a clean
   Windows box.
6. Hard invariants from the hand-off pack: the test baseline (whatever
   `dotnet test` reports on `main` today) stays green; manual intent stays
   direct via `ISpotifyPlaybackControl`; `intent_capture` mode gating
   preserved; tester endpoints (`/timeline`, `/timeline/episodes`,
   `/user-actions`) return 404 in `scenario_playback`.

## Suggested follow-up implementation issues (NOT created here)

These are suggestions for the user. Linear issues are filed only with explicit
approval. Each is one paragraph; AC is sketched, not finalized.

1. **GitHub Actions release publish workflow.** Adds a manual
   `workflow_dispatch` (or `push: tags:`-triggered) workflow that asserts
   the dispatched ref is a tag matching the versioning scheme, then runs
   the tester-drop publish (self-contained `dotnet publish` for `win-x64`,
   `client_id` injection from `SPOTIFY_TEST_CLIENT_ID`, leak-grep, zip,
   upload). AC: artifact built end-to-end on a tag; leak-grep fails the
   build on any matching pattern; no `client_secret` env var anywhere in
   the workflow; pinned to `setup-dotnet@v4` `8.0.x`; UND-33 is `Done`
   first.
2. **Code-signing setup.** Provision an EV (preferred) or OV/IV (fallback)
   Authenticode cert; store as a GitHub Actions secret pair; add a
   `signtool` step to the publish workflow. AC: shipped `GsiHost.exe` is
   signed; SmartScreen friction on a clean Windows box is measurably
   reduced compared to the unsigned baseline (EV ramps reputation faster
   than OV/IV but neither grants an instant zero-friction launch); cert
   lifecycle and renewal documented.
3. **GitHub Releases publishing (draft).** Adds a step to the publish
   workflow that promotes the artifact to a GitHub draft Release with
   release notes derived from the tag's `git log` between tags. AC:
   draft-only by default (not published); release name == tag; SHA-256 of
   the zip published in the release body for the tester README to reference.
4. **Auto-update spike.** Evaluate Velopack / Squirrel / Sparkle / a custom
   manifest-based updater against this doc's secret-handling and signing
   requirements. AC: a written recommendation (in `docs/`) on whether and
   when to add auto-update; no code change in this issue.
5. **Versioning automation.** Wire `MinVer` (or equivalent) so
   `<AssemblyInformationalVersion>` and the publish output's version metadata
   come from the git tag automatically; the host's startup checklist prints
   the version + commit SHA. AC: a fresh `dotnet publish` on a tagged commit
   produces an artifact whose `GsiHost.exe` reports the tag in
   `--version` output (or equivalent) and on the console checklist.
6. **File-logging sink at `%LOCALAPPDATA%\Undefault\logs\`.** Adds a
   `Microsoft.Extensions.Logging` file sink with the same redaction surface
   exercised by `CredentialAndTokenLoggingRedactionTests`. AC: file logs
   appear at the documented path; redaction tests cover the file-sink path;
   no token-shaped string appears in any file emitted during the UND-35
   release smoke test.
7. **Installer (Inno Setup or WiX).** Optional, when MVP audience grows.
   AC: per-machine install with a Start menu shortcut; declares the .NET 8
   runtime as a prerequisite (allowing a return to framework-dependent
   publish if we want); preserves the per-user encrypted secret store under
   `%LOCALAPPDATA%`.

The user approves these one at a time as scope; this doc does not file them.

## Out of scope for this report

- Implementing any of the items above. UND-31 is design-only.
- UND-32's packaging recipe (artifact contents, log path final pick,
  prototype `publish-tester.ps1`).
- UND-33's CI workflow code (the artifact-policy boundary above is a
  contract; the `*.yml` is UND-33's deliverable).
- UND-35's manual release checklist content (this doc's secret-handling and
  Windows UX sections feed it; the checklist itself is a separate
  deliverable).
- UND-37 territory (`Core/Adapters/*`, `Core/Music/*`, `Core/Rules/*`,
  multi-game scenarios). Untouched by this design.
