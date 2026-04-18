namespace Core.Spotify;

public sealed class SpotifyClientOptions
{
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string RedirectUri { get; init; } = "http://127.0.0.1:5292/callback";
    public string[] Scopes { get; init; } = { "user-modify-playback-state", "user-read-playback-state" };
}
