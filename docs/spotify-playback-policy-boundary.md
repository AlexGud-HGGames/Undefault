# Playback policy

Undefault controls the user’s own player. It is not a soundtrack and does not own the catalog.

Applies to Tauon, mock, and any future player.

## Do

- Pause, resume, skip, or change volume from game events
- Keep behavior local, user-configured, reversible
- Describe rules as automation (`WHEN death DO pause`)

## Do not

- Sync a track moment to a game moment (seek-to-drop, “soundtrack for CS2”)
- Mix or overlay player audio with other audio in one stream
- Build quizzes, scores, or “Spotify as gameplay”

## Check before a feature

1. Only the user’s own player?
2. Reversible and configured by the user?
3. Explainable without “sync”, “soundtrack”, or “score”?
4. No timing of a song moment to a scene?

If no → out of scope.

Spotify-specific legal reasons this product left Spotify: [spotify-constraints.md](spotify-constraints.md).
