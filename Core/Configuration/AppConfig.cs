using Core.Models;

namespace Core.Configuration;

public sealed record AppConfig(
    RulesEngineConfig RulesEngine,
    SpotifyPublicConfig Spotify,
    SpotifyActionsConfig SpotifyActions,
    GsiEndpointInfo GsiEndpoint
);

public sealed record RulesEngineConfig(
    Dictionary<EventType, List<string>> ActionMap
);

public sealed record SpotifyPublicConfig(
    string ClientId,
    string RedirectUri,
    string[] Scopes
);

public sealed record SpotifyActionsConfig(
    Dictionary<EventType, List<string>> EventPlaylistMap,
    Dictionary<EventType, int> EventVolumeMap,
    List<string> DefaultPlaylistUris,
    int? DefaultVolume
);

public sealed record GsiEndpointInfo(
    string Method,
    string Path,
    string? Url
);
