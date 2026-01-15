using System;

namespace UI.Models;

public record UiStatusSnapshot(
    string GsiStatus,
    string Game,
    string LastEvent,
    string SpotifyStatus,
    string PlaybackState,
    DateTimeOffset? LastSnapshotAt
);
