using Core.Models;
using Core.Spotify;
using Microsoft.Extensions.Logging;

namespace Core.Actions.Spotify;

public sealed class SpotifyResumeAction : AsyncEventActionBase
{
    private readonly ISpotifyClient _spotifyClient;

    public SpotifyResumeAction(
        ISpotifyClient spotifyClient,
        ILogger<SpotifyResumeAction> logger)
        : base(logger)
    {
        _spotifyClient = spotifyClient;
    }

    public override string Key => "spotify.resume";

    protected override async Task ExecuteAsync(NormalizedEvent normalizedEvent)
    {
        try
        {
            var isAuthenticated = await _spotifyClient.IsAuthenticatedAsync();
            if (!isAuthenticated)
            {
                Logger.LogWarning("Spotify not authenticated, skipping resume action");
                return;
            }

            await _spotifyClient.ResumeAsync();
            Logger.LogInformation("Spotify resumed due to event {EventType}", normalizedEvent.Type);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to resume Spotify for event {EventType}", normalizedEvent.Type);
        }
    }
}
