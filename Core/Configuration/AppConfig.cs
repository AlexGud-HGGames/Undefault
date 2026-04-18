using System;
using System.Linq;

namespace Core.Configuration;

public sealed record MusicProfilesConfig(
    string? ActiveProfileId,
    List<MusicProfile> Profiles
);

public sealed record MusicProfile(
    string Id,
    string Name,
    List<EventTrackRule> Rules
)
{
    public EventTrackRule? FindRule(string eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
        {
            return null;
        }

        return Rules.FirstOrDefault(rule =>
            string.Equals(rule.EventKey, eventKey, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record EventTrackRule(
    string EventKey,
    List<string> Tracks
);
