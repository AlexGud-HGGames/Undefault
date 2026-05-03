# Spotify Playback Policy Boundary

This project treats Spotify as the user's own local playback device, not as a synchronized game soundtrack.

The product goal is:

- read local gameplay state from CS2 GSI
- detect attention-sensitive gameplay moments
- duck, restore, pause, or resume the user's active Spotify playback
- keep all behavior local, user-configured, and reversible

## Allowed Product Shape

The safe default product shape is **local playback control**:

- `round_start` can duck volume when gameplay needs attention
- `death` can restore the user's previous volume
- future safety states can pause, resume, duck, restore, or clamp volume
- rules should describe user attention or safety, not musical synchronization
- config should stay explicit and user-controlled

Recommended wording:

- "local playback control"
- "duck, pause, or resume the user's Spotify playback"
- "game-state-aware volume control"
- "attention-aware local automation"

## Avoided Product Shape

Do not describe or design the project as Spotify content synchronized to gameplay.

Avoid:

- "Spotify soundtrack for CS2"
- "sync Spotify music to gameplay"
- "gameplay-synchronized Spotify tracks"
- "make the drop hit on round start"
- selecting or seeking tracks so specific song moments align with game moments
- rebroadcasting, remixing, recording, or exposing Spotify audio to other users

This boundary exists because Spotify's Web API Player endpoints include policy notes against synchronizing Spotify content with external visual media. UndefaultIt should stay on the local control side: it changes playback state for the current user, it does not make Spotify content part of the game timeline.

## Feature Rules

Default and MVP behavior should prefer:

- `spotify.control_profile`
- `duck`
- `restore_volume`
- `pause`
- `resume`

Use extra caution with:

- `spotify.profile`, because it can start specific tracks from events
- Smart Track Start, because `position_ms` can look like aligning song moments to game moments
- future profile packs that choose music by map, phase, bomb state, kill streak, or clutch state

If a future feature starts or seeks Spotify tracks, it must be framed as user-selected local playback behavior, not synchronized scoring. Prefer disabling or deferring any feature whose value depends on aligning a specific part of a track to a specific game event.

## Engineering Guardrail

When adding a new Spotify-facing feature, ask:

1. Does this only control the user's own active Spotify playback?
2. Is the behavior reversible and user-configured?
3. Can the feature be explained without using "sync", "soundtrack", or "score"?
4. Does the feature avoid timing Spotify content to a game scene or event?
5. Does it avoid broadcasting or sharing Spotify audio?

If the answer to any question is "no", redesign the feature toward local playback control or document why it is out of scope.
