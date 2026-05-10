# Spotify OAuth & Secrets Model (UND-34)

Research / design only. No OAuth code is changed by this issue. Implementation lands in separately-filed follow-up issues, listed at the end and **not created here**.

This is the secret-handling and OAuth-flow contract that downstream issues — UND-31 (release pipeline), UND-32 (Windows packaging), UND-35 (release checklist), UND-33 (CI implementation) — must enforce.

Inputs read for this report (no edits):

- `Core/Spotify/SpotifyOAuthService.cs`
- `Core/Spotify/SpotifyClient.cs`
- `Core/Spotify/SpotifyClientOptions.cs`
- `Core/Spotify/InMemoryTokenStorage.cs`
- `GsiHost/Services/ConsoleLaunchBootstrap.cs`
- `GsiHost/appsettings.json`
- `docs/spotify-developer-compliance-notes.md`
- `docs/spotify-playback-policy-boundary.md`
- `docs/backend-architecture.md`
- Spotify Developer documentation, Authorization Code with PKCE flow ([tutorial](https://developer.spotify.com/documentation/web-api/tutorials/code-pkce-flow), [concepts: Which OAuth flow should I use?](https://developer.spotify.com/documentation/web-api/concepts/authorization)).

## Today's behavior, in one paragraph

`SpotifyOAuthService` runs the **classic Authorization Code flow** with `client_id` + `client_secret` in an HTTP Basic auth header to `https://accounts.spotify.com/api/token`. The redirect URI must be a loopback IP literal (`http://127.0.0.1:5292/callback` by default; `localhost` is rejected by `GetRedirectUri()`). Tokens come back from `ExchangeCodeForTokenAsync` and `RefreshTokenAsync` and are held in `InMemoryTokenStorage`, which is process-local — every host restart re-prompts OAuth. Spotify app credentials reach `SpotifyOAuthService` via `SpotifyClientOptions` populated by `ConsoleLaunchBootstrap` from one of: `CLIENT_ID` / `CLIENT_SECRET` env vars → encrypted Windows secret store (`WindowsProtectedSpotifySecretStore`, DPAPI) → `appsettings.json` `Spotify.ClientSecret` → interactive console prompt; the prompt result is persisted to the encrypted store.

## Recommended OAuth flow

**Switch from Authorization Code (with `client_secret`) to Authorization Code with PKCE (no `client_secret`).** Confidential clients aren't a fit for a Windows desktop app distributed to testers and end users. PKCE is what Spotify currently recommends for that case.

Direct quote from Spotify's *Which OAuth flow should I use?* ([Web API concepts: authorization](https://developer.spotify.com/documentation/web-api/concepts/authorization)):

> The Authorization code flow is recommended for server-side applications where the client secret can be stored securely. For mobile, desktop, or browser-based apps where secrets cannot be safely stored, the Authorization code with PKCE flow is preferred to prevent interception.

And from the [PKCE tutorial](https://developer.spotify.com/documentation/web-api/tutorials/code-pkce-flow):

> The authorization code flow with PKCE is the recommended approach for mobile applications, single-page web apps, and any environment where a client secret cannot be stored securely.

Undefault is a Windows desktop client distributed as a binary — there is no environment in which a `client_secret` shipped inside the artifact stays secret. PKCE removes the need to ship one at all. Granted scopes are unchanged from `SpotifyClientOptions.Scopes` (`user-modify-playback-state`, `user-read-playback-state`).

What this changes in code (filed as a follow-up issue, not here):

- Generate a 43–128 char `code_verifier` per auth attempt; SHA-256 → base64url → `code_challenge`.
- `/spotify/authorize` URL adds `code_challenge_method=S256` and `code_challenge=<challenge>`. The verifier is held in process memory (or a short-lived per-attempt store) and consumed at `/callback`.
- `ExchangeCodeForTokenAsync` POST body adds `client_id` and `code_verifier`; **drops the `Authorization: Basic <base64(id:secret)>` header**.
- `RefreshTokenAsync` POST body adds `client_id`; drops the Basic header.
- `SpotifyClientOptions.ClientSecret` and `appsettings.json` `Spotify.ClientSecret` become unused. Remove or hard-empty them; `ConsoleLaunchBootstrap` stops prompting for / persisting a secret.

What does **not** change:

- Redirect URI rules (loopback IP literal, HTTP allowed only on loopback).
- Scopes.
- Token refresh semantics (refresh token still issued, refresh still required).
- The console-first UX — it gets simpler (no more "Spotify Client Secret" prompt or encrypted-secret-store write for the secret half).

## Spotify Developer app registrations

The project needs more than one registered app, separated by purpose, so a leak or revocation in one channel never impacts another. **Two registrations from the start, with provision for a third.**

| Registration | Purpose | `client_id` distribution | `client_secret` |
|---|---|---|---|
| `undefault-dev` | Local developer iteration. Each contributor can register their own personal copy *or* share a project-owned dev app. Used with `--use-real-spotify` against a developer's personal Spotify account. | Embeddable in dev environment (env var, `appsettings.Development.json`, or developer's encrypted store). Acceptable to live in a contributor's `appsettings.Development.json` because PKCE means no companion secret. Never in a tester artifact. | None (PKCE). |
| `undefault-test` | Internal-tester drop. The `client_id` shipped to internal testers in the first artifact (UND-32). Used with the tester's own Spotify Premium account. | **Embedded in the tester artifact** (compiled-in or in a sanitized `appsettings.json` that ships in the zip). This is acceptable under PKCE because a `client_id` is not a secret. Spotify docs treat it as a public identifier. | None (PKCE). |
| `undefault-prod` *(future)* | Public / wider-audience release. Filed only when we move beyond an invite-only tester drop. Spotify Quota Extension Request belongs here, not against `undefault-test`. | Embedded in the public artifact. Subject to Spotify's branding guidelines (`docs/spotify-developer-compliance-notes.md` §"Branding, Attribution, and Naming") at that point. | None (PKCE). |

Rationale for two-from-day-one rather than one shared:

- Spotify rate-limits and quota apply per `client_id`. Tester traffic shouldn't compete with developer-loop traffic, and developer experimentation shouldn't accidentally tilt the test app's metrics.
- If a tester's `client_id` ever needs to be rotated (compromised, accidentally bound to the wrong contact email, quota cap hit, Spotify takes enforcement action), the dev loop keeps working unchanged.
- `docs/spotify-developer-compliance-notes.md` §"Quotas, Security Codes, and Operations" already calls out "One Security Code (Client ID) per SDA. Do not reuse the UndefaultIt Client ID for unrelated experiments; register a separate dev app." This codifies that rule.

`client_id` is not secret in the cryptographic sense — it appears in every authorization URL the user opens — so embedding it in the artifact is fine, but each registration's owner contact info must stay current per Spotify Terms.

## Redirect URI strategy

**Keep the current rule: a fixed loopback IP literal on a fixed port. Default `http://127.0.0.1:5292/callback`.** Reasons:

1. The Spotify dashboard requires registered redirect URIs to match exactly. A random per-launch port would force pre-registering a range or wildcard, which Spotify does not support; pre-registering many ports is fragile.
2. `127.0.0.1` (not `localhost`) is enforced today by `SpotifyOAuthService.GetRedirectUri()` — Spotify's docs and our compliance notes both call out the loopback IP literal rule. Keep it. The PKCE migration does not change this.
3. Custom URI schemes (e.g. `undefault://callback`) are not used. They require Windows registry registration on install (an installer step we're not yet doing in MVP — see UND-32) and add SmartScreen / browser-handler friction. Loopback HTTP avoids both.

Per-environment URIs:

| Registration | Registered redirect URIs |
|---|---|
| `undefault-dev` | `http://127.0.0.1:5292/callback` (default port). Optionally also `http://127.0.0.1:5293/callback` for parallel dev runs. |
| `undefault-test` | `http://127.0.0.1:5292/callback`. |
| `undefault-prod` *(future)* | `http://127.0.0.1:5292/callback`, plus any alternate ports we explicitly support. |

Windows constraints to surface in the tester checklist (UND-35):

- **Firewall.** Kestrel's first bind to `127.0.0.1:5292` does *not* trigger the public-network firewall prompt because loopback binds are exempt by default on Windows. If a contributor changes the bind to `0.0.0.0` (we shouldn't, ever) the prompt reappears. UND-35 should explicitly verify the artifact binds loopback only.
- **Port collision.** If `5292` is already in use Kestrel fails to start. UND-31 / UND-32 should document the override (`Gsi:Url` in `appsettings.json` / config override) and have UND-35 include a "port-busy" recovery step.
- **SmartScreen.** Unsigned tester binaries hit SmartScreen on first launch. SmartScreen has nothing to do with OAuth, but a tester who can't get past SmartScreen never sees `/callback`. UND-31 owns the code-signing decision; this doc only flags the dependency.

## Token storage recommendation

**Keep `InMemoryTokenStorage` for the first internal-tester drop. Plan a persistent encrypted token store as a separately-filed follow-up issue.** Rationale and constraints below.

Today: `InMemoryTokenStorage` is a process-local, lock-protected holder; `ClearTokensAsync` zeros it; access tokens vanish on host restart and the user re-auths. From `docs/backend-architecture.md` Real Mode constraints: "token storage is still process-local, so auth must be repeated after restart."

Why keep this for the first drop:

- It is the most conservative default per `docs/spotify-developer-compliance-notes.md` §"Token storage" ("Today we keep them in memory only … which is the most conservative option").
- Tokens are Spotify Personal Data. Persisting them brings Appendix A §6 (encryption-at-rest, user-controlled deletion) and §5 (delete within 5 days of disconnect) firmly into scope. Designing the persistent store properly is a separate piece of work.
- Tester UX cost is low: re-auth on host start is a single browser tab. Acceptable for an invite-only tester drop.

Why we'll need a persistent store later (filed as a follow-up):

- For day-to-day product use, re-auth on every restart is friction. Once we go beyond the first internal drop, we want refresh tokens to survive restarts.

The persistent-store design (when filed) must, at minimum:

- Encrypt at rest using DPAPI `CurrentUser` scope (matches the existing `WindowsProtectedSpotifySecretStore` pattern). Path under `%LOCALAPPDATA%\Undefault\` (UND-32 owns the exact path).
- Store only `access_token`, `refresh_token`, `expires_at`, and granted scopes. No `client_id`, no `client_secret` (PKCE has none), no profile data.
- Wipe atomically on disconnect: `ITokenStorage.ClearTokensAsync` deletes the encrypted file. This satisfies the "delete within 5 days" rule at the user's command.
- A `--clear-spotify-tokens` console flag, parallel to today's `--clear-spotify-secrets`. Optional: combined `--logout` flag that clears both.
- No log line ever contains an access or refresh token. No token written to plaintext anywhere on disk.

Logout / revocation semantics (also part of the follow-up issue):

- `--logout` (or its equivalent) clears the persistent token store and stops the host from using the previous identity until the user re-auths.
- Spotify does not currently expose a token revocation endpoint via the Web API. The persistent store delete is the operational logout. Document this in the future store's design.
- If a Spotify call returns 401 with `"reason": "INVALID_AUTH"` or 403 with persistent reauth required, the host clears the token cache and falls back to the conservative state (no playback control); it does **not** retry-loop. This is already the spirit of `docs/failure-safety-spec.md`.

## CI / local / artifact secret split

**Nothing OAuth-secret may live in the repo, the artifact, or any log.** The secret split below assumes the PKCE migration has happened (no `client_secret` anywhere). Until PKCE lands, the same rules apply with one extra column ("`client_secret`"); see "Transition rules" below.

| Value | Repo | Tester artifact | CI (GitHub Actions) | Local developer config | Tester local machine | Logs |
|---|---|---|---|---|---|---|
| `client_id` (`undefault-dev`) | OK in `appsettings.Development.json` if and only if it's a registration only the contributor controls. | Never. | Never (CI doesn't run real OAuth). | Yes (env var `CLIENT_ID`, or per-developer `appsettings.Development.json` ignored by `.gitignore`). | Never (testers don't use the dev app). | Never. |
| `client_id` (`undefault-test`) | OK to commit only if the registration is project-owned, the URL is a fixed loopback URI, and the workspace agrees. Default position: do **not** commit; inject at publish time. | Yes (sanitized `appsettings.json` shipped in the zip carries the `undefault-test` `client_id` only). | Yes (GitHub Actions secret, used by the publish workflow to write the value into the shipped `appsettings.json` at publish time, if we follow the "inject at publish" path). | No. | Yes (lives in the artifact's `appsettings.json`). | Never (already enforced by current logging — a `client_id` line in startup logs is a leak). |
| `client_secret` (any registration) | **Never.** Repo `.gitignore` already protects user-local `secrets.json` style files; reinforce in tester docs. | **Never.** PKCE means there is no secret to ship. | **Never.** Even after the PKCE switch we have no need for `client_secret` in CI. If a transition build still needs it, it lives in a GitHub Actions secret and is never written to disk except via `dotnet publish` substitution; better is to skip CI publishing real-OAuth artifacts entirely (consistent with hand-off pack #2's "no CI publishing in MVP"). | Local-only, in env var or DPAPI-encrypted store; **never** committed. | **Never** in artifact. After PKCE: not present at all. | **Never.** |
| `access_token`, `refresh_token` | Never. | Never. | Never. | Never on disk in plaintext. Memory-only today; future encrypted store under `%LOCALAPPDATA%`. | Memory-only today; future DPAPI-encrypted file under `%LOCALAPPDATA%\Undefault\`. | **Never.** Includes redaction of HTTP request/response bodies in any future debug logging. |
| `code_verifier` (PKCE, transient) | Never. | Never. | Never. | Process memory only, scoped to a single auth attempt; freed once exchanged. | Process memory only. | Never. |

GitHub Actions secrets we expect to use (filed under UND-33's CI implementation, suggested only):

- *(Optional, only if we choose "inject `client_id` at publish time")* `SPOTIFY_TEST_CLIENT_ID` — the `undefault-test` `client_id` substituted into the published `appsettings.json` at build time.
- *(Future, code-signing)* `WINDOWS_CODE_SIGN_CERT`, `WINDOWS_CODE_SIGN_PASSWORD` — UND-31 owns the timing decision.

Local-developer environment expectations (no secrets enter CI through these paths):

- `CLIENT_ID` env var (PKCE) — the developer's own `undefault-dev` client id.
- *(Until PKCE lands)* `CLIENT_SECRET` env var — the developer's own `undefault-dev` client secret. Never committed.

Transition rules until PKCE lands:

- The current Authorization Code flow code path still needs `client_secret` operationally. During the transition window, every rule in this matrix applies as written, with the additional rule that the secret must be DPAPI-encrypted at rest on developer/tester machines (via `WindowsProtectedSpotifySecretStore`) and never appear in `appsettings.json` at rest in the repo or artifact.
- The PKCE follow-up issue is the path that lets us drop the column entirely.

## Security checklist (the contract for UND-31 / UND-32 / UND-33 / UND-35)

Mandatory checks. Failing any one is a release blocker.

1. **No `client_secret` in the repo.** `git grep` for `client_secret`, `ClientSecret`, `CLIENT_SECRET` over the working tree must return only code identifiers, never literal values. (Already true today; this fixes it as policy.)
2. **No `client_secret` in the artifact.** A grep across the published output (zip contents) for `ClientSecret` returns no hits with a non-empty value. After PKCE, `Spotify.ClientSecret` is removed from the shipped `appsettings.json` outright.
3. **No tokens in the artifact.** A grep across the published output for `access_token`, `refresh_token` returns no hits.
4. **No client credentials in logs.** Startup logs must not echo `client_id`, `client_secret`, `access_token`, `refresh_token`. The console checklist may say "Spotify credentials present" / "missing" and may show the redirect URI; it must not show the values.
5. **Redirect URI is a loopback IP literal.** `SpotifyOAuthService.GetRedirectUri()` already enforces this; UND-35's checklist explicitly verifies it on a clean machine.
6. **Encrypted secret store on Windows only.** `ConsoleLaunchBootstrap.Apply` already throws `PlatformNotSupportedException` for non-Windows. Keep this as the explicit boundary; do not cross-compile fallbacks that would weaken at-rest protection.
7. **`--clear-spotify-secrets` works.** The flag deletes the DPAPI-encrypted secret file. Verified in `Cs2Simulator.Tests` / host bootstrap tests. Until persistent token storage lands, "logout" = `--clear-spotify-secrets` + restart.
8. **CI does not run real OAuth.** No GitHub Actions job calls `accounts.spotify.com`. Tests use `MockSpotifyClient` or unit-test seams (`InMemoryTokenStorage`, fake `IHttpClientFactory`).
9. **Scopes are minimal.** Only `user-modify-playback-state` and `user-read-playback-state` are requested. New scopes require a documented justification and re-prompt; do not bundle.
10. **Token storage is encrypted at rest *or* in-memory.** No plaintext token file on disk under any future change. The first tester drop ships in-memory (default `InMemoryTokenStorage`); the persistent-store follow-up uses DPAPI under `%LOCALAPPDATA%\Undefault\`.
11. **Token file deletion on disconnect is atomic.** When persistent storage lands, `ClearTokensAsync` removes the file before returning. Satisfies Appendix A §5 "delete within 5 days" at user request.
12. **Disconnect does not leak.** No `Spotify-Personal-Data` reference (track titles, device names, user display name, country) is persisted or logged. Per `docs/spotify-developer-compliance-notes.md` §"Don't store what you can fetch."
13. **Tester checklist verifies items 1–11 explicitly.** UND-35's manual checklist must include the grep-style checks (1, 2, 3, 4) on the actual artifact, not on the source tree.
14. **Auto-update (when added) does not change the secret model.** A future updater pushes only published artifacts; it does not deliver `client_id` overrides at runtime. UND-31's auto-update decision references this row.

## Suggested follow-up implementation issues (NOT created here)

These are suggestions for the user. Linear issues are filed only with explicit approval.

1. **Switch Spotify OAuth from Authorization Code to PKCE.** AC: `SpotifyOAuthService` no longer reads `client_secret`; PKCE verifier/challenge generated per auth attempt; `Spotify.ClientSecret` removed from `appsettings.json`; `ConsoleLaunchBootstrap` no longer prompts for or stores a client secret; existing tests pass; new tests cover PKCE round-trip and Basic-header absence.
2. **Persistent encrypted token store.** AC: a new `ITokenStorage` implementation backed by DPAPI (`CurrentUser`) under `%LOCALAPPDATA%\Undefault\`; round-trip across restarts; `--clear-spotify-tokens` flag wipes the file; not enabled by default until UND-31's checklist signs off.
3. **Spotify app registration plan.** Operational, not code: register `undefault-test` (and confirm the `undefault-dev` ownership story) before the first tester drop. AC: redirect URIs registered, contact email accurate, scopes match `SpotifyClientOptions.Scopes`, internal-test users explicitly added to the test app's user list under Spotify's *Development Mode* limit.
4. **Logging redaction tests.** AC: a regression test confirms no log line emitted by `SpotifyOAuthService`, `SpotifyClient`, `ConsoleLaunchBootstrap`, or `Cs2SetupService` contains the configured `client_id`, `client_secret`, or any token-shaped string.
5. **`--logout` flag.** AC: `--logout` clears both the encrypted secret store (where applicable) and the persistent token store (when item 2 lands), and prints a single line confirming the wipe.
6. **Quota-extension preparation for `undefault-prod`.** Operational. Filed only when we plan a wider release.

## Out of scope for this report

- Implementing any of the OAuth code changes above. UND-34 is research / design only.
- Production rollout, marketing site, EULA / privacy policy authoring (those are referenced in `docs/spotify-developer-compliance-notes.md` §"Open Questions / Action Items" and are owned separately).
- UND-37 territory (`Core/Adapters/*`, `Core/Music/*`, `Core/Rules/*`, etc.). Untouched.
- Mock mode (`MockSpotifyClient`) — unaffected by everything above; mock mode never holds real Spotify credentials.
- Storing Spotify Content (track names, audio features, etc.) — explicitly forbidden by `docs/spotify-developer-compliance-notes.md` §"Caching & Storage of Spotify Content"; not introduced by this design.
