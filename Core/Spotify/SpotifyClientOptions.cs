namespace Core.Spotify;

/// <summary>
/// Spotify OAuth client configuration. As of UND-47 the flow is Authorization Code with
/// PKCE; no <c>client_secret</c> is held, persisted, or sent on the wire, so the
/// previous <c>ClientSecret</c> property has been removed.
/// </summary>
public sealed class SpotifyClientOptions
{
    public string ClientId { get; init; } = string.Empty;
    public string RedirectUri { get; init; } = "http://127.0.0.1:5292/callback";
    public string[] Scopes { get; init; } = { "user-modify-playback-state", "user-read-playback-state" };
}
