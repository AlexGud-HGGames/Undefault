using Core.Models;
using Core.Spotify;
using Microsoft.Extensions.Logging;

namespace Core.Actions.Spotify;

public sealed class SpotifyPauseAction : AsyncEventActionBase
{
    private readonly ISpotifyClient _spotifyClient;

    public SpotifyPauseAction(
        ISpotifyClient spotifyClient,
        ILogger<SpotifyPauseAction> logger)
        : base(logger)
    {
        _spotifyClient = spotifyClient;
    }

    public override string Key => "spotify.pause";

    protected override async Task ExecuteAsync(NormalizedEvent normalizedEvent)
    {
        try
        {
            var isAuthenticated = await _spotifyClient.IsAuthenticatedAsync();
            if (!isAuthenticated)
            {
                Logger.LogWarning("Spotify not authenticated, skipping pause action");
                return;
            }

            await _spotifyClient.PauseAsync();
            Logger.LogInformation("Spotify paused due to event {EventType}", normalizedEvent.Type);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to pause Spotify for event {EventType}", normalizedEvent.Type);
        }
    }
}
