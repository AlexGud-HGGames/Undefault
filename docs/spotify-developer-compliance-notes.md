# Spotify Developer Compliance Notes

This is a curated, project-specific reading of the three Spotify developer
legal documents:

- [Developer Terms](https://developer.spotify.com/terms) (v10, 15 May 2025)
- [Developer Policy](https://developer.spotify.com/policy) (15 May 2025)
- [Compliance Tips](https://developer.spotify.com/compliance-tips)

It calls out only what materially constrains UndefaultIt. It does not replace
the documents; it is what we should keep visible while building. For product
framing, it pairs with `[docs/spotify-playback-policy-boundary.md](spotify-playback-policy-boundary.md)`.

## How UndefaultIt Maps to Spotify's Definitions

- We are an **SDA** (Spotify Developer Application): a local Windows backend
that calls the Spotify Web API to pause, resume, play, and change volume.
- We are a **Streaming SDA**. The Compliance Tips explicitly classify any app
using `/v1/me/player/play`, `/v1/me/player/next`, `/v1/me/player/previous`,
or "any other Spotify API or SDK to control playback of music or stream
music" as a Streaming SDA. Our default `spotify.control_profile` flow uses
these endpoints, so the stricter Streaming SDA rules apply.
- The user is the **Approved Device** owner (desktop / laptop). UndefaultIt
controls a background Spotify application on the user's own device.
- We process **Spotify Personal Data** (the linked user's account, tokens,
and current playback state), so the Data Protection Appendix applies.

## Top Risks for This Project

These are the policy clauses most likely to be invoked against an app like
ours. Treat them as hard guardrails.

### 1. No synchronization of recordings with visual media — Policy III.7

> Do not synchronize any sound recordings with any visual media, including
> any advertising, film, television program, slideshow, video, or similar
> content.

CS2 gameplay is "visual media." Aligning a song's beat, drop, or position to
in-game events would violate this clause. This is the single biggest reason
we frame the product as **local playback control, not a game soundtrack**
(see `[spotify-playback-policy-boundary.md](spotify-playback-policy-boundary.md)`).

Direct implications:

- Smart Track Start (`position_ms`) must stay framed as user-chosen track
start offsets, not "make the drop hit on round start." Keep the existing
rule that it only affects `spotify.profile`, never duck/pause/resume/restore.
- Future profile packs that pick tracks from map / phase / kill-streak / clutch
state are dangerous and need explicit review before shipping.

### 2. No mix / segue / overlap with other audio — Policy III.8

> Do not permit any device or system to segue, mix, re-mix, or overlap any
> Spotify Content with any other audio content (including other Spotify
> Content).

We must not play game audio "underneath" Spotify, layer SFX on top of a
track, or crossfade Spotify with anything else. Volume ducking against the
*game's own* audio engine is fine because we only change Spotify's own
volume; we never combine Spotify audio with game audio in a single mixed
output stream.

### 3. We are not a game, and must not become one — Policy III.3

> Do not create a game, including trivia quizzes.

UndefaultIt observes CS2 but is not itself a game. Keep it that way:

- No quiz / "name that tune" / score / win-state behavior.
- Do not market the SDA as adding gameplay. It augments the user's existing
Spotify session while *they* play CS2.

### 4. No voice control — Policy III.4

Do not add "OK Spotify" / voice-trigger features. Hotkeys and the timeline
manual-intent API are fine; voice command pipelines are not.

### 5. No ringtones, alarms, alert tones — Policy III.2

We must not let users wire Spotify clips into a Windows alarm, notification
sound, or alert tone via our backend.

### 6. No analytics, profiling, listenership metrics — Policy III.14

> Do not analyze the Spotify Content or the Spotify Service for any purpose,
> including … creating new or derived listenership metrics, benchmarking,
> functionality, usage statistics, user metrics, or building profiles of users …

The manual intent timeline and `/timeline/episodes` are designed for the
**tester / product owner** (per the workspace rule). They must not be turned
into a "what do players listen to" research product or a public dataset. Keep
timeline output local to the running host.

### 7. No AI / ML training on Spotify data — Terms IV.1, Policy III.15

Do not feed Spotify metadata, audio, audio features, lyrics, or playback
history into model training, fine-tuning, or embedding pipelines. Manual
timeline data is fine for *human* scenario discovery; it is not training
data for an automated rule generator that learns from Spotify content.

### 8. No commercial use of a Streaming SDA — Policy IV

For Streaming SDAs the policy bans:

1. selling the SDA, the Spotify Platform, Spotify Content, or access thereto;
2. in-app payment / monetization;
3. selling advertising, sponsorships, or promotions on the SDA itself.

Concrete consequence: we cannot charge for UndefaultIt as long as it controls
Spotify playback. We cannot put ads, donation tiers, or paid feature unlocks
in the host or any future client. Limited commercial use is only allowed for
**Non-Streaming** SDAs, which we are not.

### 9. Personal, non-commercial use only — Policy III.11

UndefaultIt is for an individual user on their own machine. We must not
target retail / bar / restaurant / "shoutcast room" / esports-venue use
cases.

## OAuth, Scopes, and Personal Data

Sourced from Compliance Tips ("Focus on the User") and Developer Terms
Section V + Appendix A.

### Scopes

- Request **only** the scopes we actually use today (playback control,
current playback, volume). Do not over-scope "in case we need it later."
- If a future feature needs a new scope, re-prompt the user; do not bundle
it into the initial OAuth request.
- Make it clear in our docs and the bootstrap console why each scope is
requested.

### Token storage

- OAuth access and refresh tokens are Spotify Personal Data. Today we keep
them in memory only (per README "Important Limits"), which is the most
conservative option.
- If we ever persist tokens, encryption-at-rest plus user-controlled
deletion are required by Appendix A §6 ("Information Security Practices").
- Spotify app credentials (`CLIENT_ID` / `CLIENT_SECRET`) belong to the
developer, not the end user, and Terms VI.4 makes us responsible for
keeping them confidential. The encrypted Windows secret store is the
right path; never log them.

### Privacy policy & EULA — Terms V

Even though there is "no separate desktop UI," if we ship UndefaultIt to
anyone other than ourselves we must surface:

- a **privacy policy** describing what Spotify data we touch, how we use
it, retention, and contact info;
- an **end-user agreement** that:
  - disclaims warranties on Spotify's behalf,
  - prohibits reverse-engineering / derivative works of Spotify Platform /
  Service / Content,
  - names Spotify as a third-party beneficiary,
  - puts liability on us, not Spotify.

Today this is satisfied implicitly because the user is the developer. The
moment we hand the host to a second person, this becomes mandatory.

### Account disconnect

- Provide a clear, self-serve way to disconnect Spotify and delete tokens.
- Per Appendix A §5, when the user disconnects (or Spotify requests it), we
must stop accessing their data and **delete it within 5 days**.
- `--clear-spotify-secrets` already covers credentials. We should make sure
any future cached personal data (current playback snapshots, last device
id, last saved volume per user) is wiped on disconnect.

### Don't store what you can fetch — Compliance Tips

Spotify recommends fetching profile info, display name, country, etc.,
on demand instead of caching. For us, this means:

- the "last saved volume" we use for `restore_volume` is acceptable
because it is operational state, not user profile data;
- avoid persisting playlists, track names, artist names, listening history,
or device lists beyond the lifetime of the host process.

## Caching & Storage of Spotify Content — Terms IV

> You may not store, aggregate or create compilations or databases of Spotify
> Content … Do not store Spotify Content indefinitely.

Allowed temporary caching:

- metadata and cover art (we currently use neither beyond URI strings);
- Conditional Downloads of sound recordings — Premium-only, time-limited
offline syncing. We do **not** do this and should not start.

Concrete rules for our codebase:

- Do not snapshot Spotify catalog data into JSON files in the repo or in
user content roots.
- The timeline JSONL must record our normalized events and our manual
intent records. It must not become a log of Spotify track titles, album
art, or audio features. URIs are fine as opaque identifiers.

## Branding, Attribution, and Naming — Policy II, VI; Terms III

- If/when we add a UI that displays Spotify Content (track name, album,
cover art), we must:
  - attribute Spotify (Spotify mark + the [Branding Guidelines](https://developer.spotify.com/documentation/design));
  - link back to the track / album / playlist on Spotify;
  - show metadata and cover art **together** with playback for any
  Streaming surface (Policy III.1);
  - never modify, crop, or restyle cover art.
- The product name is "UndefaultIt." It does not start with "Spot" and is
not confusably similar to "Spotify." Keep it that way; do not rename to
anything beginning with "Spot-" or implying Spotify endorsement.
- We must not use Spotify marks in our company name, domain, or trademarks.

## Quotas, Security Codes, and Operations — Terms VI, IX

- One Security Code (Client ID) per SDA. Do not reuse the UndefaultIt
Client ID for unrelated experiments; register a separate dev app.
- Keep registered contact info accurate. Spotify can revoke access if they
cannot reach us.
- If we apply for an extended quota, we must use that quota only for the
reviewed use case and re-notify Spotify if the use case changes.
- Spotify can suspend / end access to permissions our SDA has not used in
90 days (Terms IX.8.5). If an OAuth scope is rarely exercised, we may
silently lose it; design for re-auth.
- Spotify can take enforcement action with or without notice. Build
defensively: if Spotify calls return 401/403 unexpectedly, fall back to
the documented safe state (Spotify off / unchanged), do not retry-loop.

## Premium and Active Device

Spotify confirms that real playback control via the Web API requires Premium
and an active playback device. The README already states this. Compliance-
wise, this means:

- Free-tier users will hit failures, not policy violations. Surface that
clearly to the user.
- Do not try to work around Free restrictions (e.g., pretend to be Premium,
inject ad-skip behavior, stream-rip). Terms IV "Unauthorized access" and
"stream ripping" are flat bans.

## Mandatory Operational Behaviors

A short list of "always-on" behaviors implied by the documents:

- **One side effect per evaluation tick.** Already enforced by the workspace
rule (`product-boundaries`), and aligned with Policy III "Respect Content"
  - Terms IV "interfering with the proper functioning of the Spotify
  Service" by avoiding duplicated calls.
- **Safety overrides adaptivity.** A `Danger` state must clamp gain /
restore / pause as documented in `music-safety-state-spec.md`. This is
also our defense against accidental synchronization-style behavior.
- **No retry storms.** Policy IV bans "excessive service calls that are not
strictly required." Our processing pipeline must coalesce events and back
off on Spotify errors.
- **Logs scrub credentials.** No `CLIENT_SECRET`, no access token, no
refresh token in logs (mock or real).
- `**Hotkeys + Timeline` is internal.** Per workspace rule, manual intent
is for testers and the product owner. It must not become a public
feature that profiles users or aggregates listening behavior.

## Open Questions / Action Items

These are concrete follow-ups suggested by reading the three documents
against the current README and docs:

1. **Privacy policy + EULA stub.** Author short, plain-English versions
  under `docs/legal/` covering: data we touch, where it is stored, how
   to disconnect, Spotify as third-party beneficiary, no warranty. Required
   the moment a second user runs UndefaultIt.
2. **Disconnect & delete flow.** Verify `--clear-spotify-secrets` plus the
  in-process token wipe satisfies the "delete within 5 days" rule. If we
   later cache profile data, add an explicit disconnect routine.
3. **Smart Track Start review.** Document why `position_ms` is *not*
  gameplay synchronization (user-chosen track start offsets), and add a
   compliance check to the Smart Track Start design doc.
4. **Branding readiness.** Before any UI surface ships track / album / art
  data, add cover-art + metadata + back-link wiring per Branding Guidelines.
5. **Scope audit.** List the exact Spotify scopes the host requests today,
  and remove any scope not currently exercised by `spotify.control_profile`,
   `spotify.profile`, or `spotify.volume_duck`.
6. **Quota & telemetry plan.** Add backoff + coalescing tests so we cannot
  accidentally exceed reasonable call rates per Spotify's "no excessive
   service calls" rule.
7. **No-monetization note.** Add a one-liner in the README that UndefaultIt
  is non-commercial as long as it is a Streaming SDA, so future
   contributors do not propose paid tiers without re-reading Policy IV.