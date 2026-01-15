using System;

namespace Core.Models;

public sealed record StatusSnapshot(
    string GsiStatus,
    DateTimeOffset? LastSnapshotAt,
    string Game,
    NormalizedEvent? LastEvent,
    string SpotifyStatus,
    string PlaybackState
);
