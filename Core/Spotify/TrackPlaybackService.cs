using Microsoft.Extensions.Logging;

namespace Core.Spotify;

public sealed class TrackPlaybackService : ITrackPlaybackService
{
    private readonly ISpotifyClient _spotifyClient;
    private readonly ISmartTrackStartService _smartTrackStartService;
    private readonly ILogger<TrackPlaybackService> _logger;

    public TrackPlaybackService(
        ISpotifyClient spotifyClient,
        ISmartTrackStartService smartTrackStartService,
        ILogger<TrackPlaybackService> logger)
    {
        _spotifyClient = spotifyClient;
        _smartTrackStartService = smartTrackStartService;
        _logger = logger;
    }

    public async Task PlayTrackAsync(string trackUri, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackUri);

        var isAuthenticated = await _spotifyClient.IsAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        if (!isAuthenticated)
        {
            _logger.LogWarning("Spotify not authenticated, skipping track playback for {TrackUri}", trackUri);
            return;
        }

        var startPositionMs = await _smartTrackStartService
            .ResolveStartPositionMsAsync(trackUri, cancellationToken)
            .ConfigureAwait(false);

        await _spotifyClient.PlayAsync(trackUri, startPositionMs, cancellationToken).ConfigureAwait(false);

        if (startPositionMs.HasValue)
        {
            _logger.LogInformation(
                "Smart Track Start applied for {TrackUri} at {StartPositionMs}ms",
                trackUri,
                startPositionMs.Value);
        }
    }
}
