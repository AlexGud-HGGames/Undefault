# Roadmap

## MVP slice (2026-06-28)

Product owner approved the first MVP slice (Linear umbrella UND-64): connect Spotify + connect CS2 + media key → playback control (observed) + record confirmed pause/resume with timestamps + persist to JSONL. The MVP runs in `intent_capture` runtime mode via one command:

```powershell
dotnet run --project .\GsiHost -- --mvp
```

Status:

- MVP #1 Spotify connect: PKCE OAuth works; UND-52 (mandatory `state` + verifier TTL) implemented, pending commit; per-session tokens accepted for MVP.
- MVP #2 CS connect: `/gsi` ingestion + auto cfg install work; `CS2 GSI connected` console banner on first post (UND-66).
- MVP #3 Media key → playback (observed): `--mvp` enables `intent_capture` + Timeline + PlaybackObserver; the user controls playback with the keyboard media play/pause key (Spotify handles it natively) and Undefault observes + records (UND-66 / UND-78; bound hotkeys + `/user-actions` removed).
- MVP #4/#5 Record + persist: confirmed `playback_paused` / `playback_resumed` with timestamps appended to JSONL in `intent_capture`; no-ops skipped (UND-65 Done).

Post-MVP: auto-scenarios, safety/mixer engine, neutral-context as live control, multi-game/Dota, scenario rule packs, packaging, persistent tokens. See the Linear UND-37 tree and the "Later" section below.

## Current State

The active product path is now the console-first backend:

- `GsiHost` is the runtime entry point
- CS2 GSI auto-setup is working against the local host
- real Spotify OAuth is integrated and verified
- Spotify app credentials can be stored in the encrypted local secret store
- the OAuth callback is working on `http://127.0.0.1:5292/callback`
- the access token is still process-local and stored in memory after auth
- the current default gameplay behavior is `round_start -> duck volume` and `death -> restore volume`

## Current Direction

The next phase should optimize for fast backend iteration, not new UI flows.

The main goal is to make gameplay-to-music behavior easy to express from **`RulesEngine.ActionMap`**, profile JSON, and the console so you can add, test, and evolve rules without first building editor screens. (Here “scenario” means profile- and map-driven behavior, not a separate YAML orchestration layer.)

## Now

- keep the console bootstrap and host architecture stable
- keep the default music-control behavior configurable from `control-profiles.json` with routing in `RulesEngine.ActionMap`
- document the console-first setup path clearly enough that a new engineer can run and edit it without UI help
- preserve the current working CS2 ingestion, Spotify OAuth callback, and encrypted secret storage flows
- document the practical CS2 GSI event space so future profile work can target real available signals quickly

## Next

- expand the console control profile with more CS2-driven scenarios once the basic off/on/duck path is stable
- add more normalized gameplay events only when there is a concrete profile use case for them
- improve backend diagnostics around active profile selection, event routing, and Spotify playback failures
- add targeted regression coverage around config-driven scenario behavior and console startup

## Later

- persistent Spotify OAuth token storage across process restarts
- richer scenario packs around bomb flow, round conclusions, kill streaks, and low-health moments
- optional UI support for the console-defined control profile model after the backend shape proves stable
- better end-to-end automation around backend startup, CS2 setup, and Spotify authorization
- Dota 2 support only after the CS2 backend path is mature

## Guardrails

- prefer small, explicit configuration over abstraction-heavy profile systems
- keep console/file setup as the source of truth until the profile shape settles
- do not refactor the host architecture unless it clearly reduces friction for scenario development
- do not break the verified `round_start` / `death` runtime while adding the new control profile path
