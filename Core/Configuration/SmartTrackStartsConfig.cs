using System;
using System.Linq;

namespace Core.Configuration;

public sealed record SmartTrackStartsConfig(
    List<SmartTrackStartEntry> Entries
);

public sealed record SmartTrackStartEntry(
    string? TrackUri,
    string? TrackId,
    int StartPositionMs,
    string? CueLabel = null
)
{
    public bool Matches(string? trackUri)
    {
        if (string.IsNullOrWhiteSpace(trackUri))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(TrackUri)
            && string.Equals(TrackUri, trackUri, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(TrackId))
        {
            return false;
        }

        var candidateTrackId = ParseTrackId(trackUri);
        return !string.IsNullOrWhiteSpace(candidateTrackId)
            && string.Equals(candidateTrackId, TrackId, StringComparison.OrdinalIgnoreCase);
    }

    public static string? ParseTrackId(string? trackUri)
    {
        if (string.IsNullOrWhiteSpace(trackUri))
        {
            return null;
        }

        var parts = trackUri
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length < 3)
        {
            return null;
        }

        return string.Equals(parts[0], "spotify", StringComparison.OrdinalIgnoreCase)
            && string.Equals(parts[1], "track", StringComparison.OrdinalIgnoreCase)
            ? parts.Last()
            : null;
    }
}
