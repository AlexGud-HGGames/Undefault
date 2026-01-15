using Core.Models;
using Core.Spotify;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Actions.Spotify;

public sealed class SpotifySetVolumeAction : AsyncEventActionBase
{
    private readonly ISpotifyClient _spotifyClient;
    private readonly SpotifyActionOptions _options;

    public SpotifySetVolumeAction(
        ISpotifyClient spotifyClient,
        IOptions<SpotifyActionOptions> options,
        ILogger<SpotifySetVolumeAction> logger)
        : base(logger)
    {
        _spotifyClient = spotifyClient;
        _options = options.Value;
    }

    public override string Key => "spotify.volume";

    protected override async Task ExecuteAsync(NormalizedEvent normalizedEvent)
    {
        try
        {
            var isAuthenticated = await _spotifyClient.IsAuthenticatedAsync();
            if (!isAuthenticated)
            {
                Logger.LogWarning("Spotify not authenticated, skipping volume action");
                return;
            }

            var volume = GetVolumeForEvent(normalizedEvent);
            if (volume.HasValue)
            {
                await _spotifyClient.SetVolumeAsync(volume.Value);
                Logger.LogInformation("Spotify volume set to {Volume} due to event {EventType}", volume.Value, normalizedEvent.Type);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to set Spotify volume for event {EventType}", normalizedEvent.Type);
        }
    }

    private int? GetVolumeForEvent(NormalizedEvent normalizedEvent)
    {
        if (_options.EventVolumeMap.TryGetValue(normalizedEvent.Type, out var volume))
        {
            return volume;
        }

        return _options.DefaultVolume;
    }
}
