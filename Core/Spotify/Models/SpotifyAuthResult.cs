namespace Core.Spotify.Models;

public sealed record SpotifyAuthResult(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    string[] Scopes
);
