using Core.Models;

namespace Core.Configuration;

public sealed record MusicProfilesConfig(
    string? ActiveProfileId,
    List<MusicProfile> Profiles
);

public sealed record MusicProfile(
    string Id,
    string Name,
    Dictionary<EventType, EventRule> Rules
);

public sealed record EventRule(
    EventAction Action,
    List<string> Tracks,
    int? Volume
);

public enum EventAction
{
    None,
    Play,
    Pause,
    Resume
}
