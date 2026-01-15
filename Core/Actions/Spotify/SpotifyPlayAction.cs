using Core.Models;
using Core.Spotify;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Actions.Spotify;

public sealed class SpotifyPlayAction : AsyncEventActionBase
{
    private readonly ISpotifyClient _spotifyClient;
    private readonly SpotifyActionOptions _options;

    public SpotifyPlayAction(
        ISpotifyClient spotifyClient,
        IOptions<SpotifyActionOptions> options,
        ILogger<SpotifyPlayAction> logger)
        : base(logger)
    {
        _spotifyClient = spotifyClient;
        _options = options.Value;
    }

    public override string Key => "spotify.play";

    protected override async Task ExecuteAsync(NormalizedEvent normalizedEvent)
    {
        try
        {
            var isAuthenticated = await _spotifyClient.IsAuthenticatedAsync();
            if (!isAuthenticated)
            {
                Logger.LogWarning("Spotify not authenticated, skipping play action");
                return;
            }

            var uri = GetUriForEvent(normalizedEvent);
            await _spotifyClient.PlayAsync(uri);
            Logger.LogInformation("Spotify playing {Uri} due to event {EventType}", uri ?? "current", normalizedEvent.Type);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to play Spotify for event {EventType}", normalizedEvent.Type);
        }
    }

    private string? GetUriForEvent(NormalizedEvent normalizedEvent)
    {
        if (_options.EventPlaylistMap.TryGetValue(normalizedEvent.Type, out var uri))
        {
            return uri;
        }

        return _options.DefaultPlaylistUri;
    }
}
