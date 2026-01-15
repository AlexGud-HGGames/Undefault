using System;
using System.Collections.Generic;

namespace Core.Models;

public sealed record StatusSnapshot(
    string GsiStatus,
    DateTimeOffset? LastSnapshotAt,
    string Game,
    NormalizedEvent? LastEvent,
    string? LastAction,
    IReadOnlyDictionary<string, TimeSpan>? Cooldowns,
    string SpotifyStatus,
    string PlaybackState,
    string? Track
);
