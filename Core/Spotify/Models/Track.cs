namespace Core.Spotify.Models;

public sealed record Track(
    string Id,
    string Name,
    string? Uri,
    int? DurationMs,
    IReadOnlyList<Artist> Artists,
    Album? Album
);

public sealed record Artist(
    string Id,
    string Name
);

public sealed record Album(
    string Id,
    string Name,
    string? ImageUrl
);
