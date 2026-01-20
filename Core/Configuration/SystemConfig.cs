namespace Core.Configuration;

public sealed record SystemConfig(
    SpotifySystemConfig Spotify,
    GsiConfig Gsi,
    bool UseMockSpotify
);

public sealed record SpotifySystemConfig(
    string ClientId,
    string RedirectUri,
    string[] Scopes,
    string? ClientSecret
);

public sealed record GsiConfig(
    string Method,
    string Path,
    string? Url
);
