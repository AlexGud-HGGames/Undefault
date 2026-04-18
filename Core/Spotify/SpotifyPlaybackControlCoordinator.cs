using Core.Actions.Spotify;
using Core.Configuration;
using Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Spotify;

public sealed class SpotifyPlaybackControlCoordinator : ISpotifyPlaybackControl
{
    private readonly ISpotifyClient _spotifyClient;
    private readonly SpotifyVolumeDuckOptions _duckOptions;
    private readonly ILogger<SpotifyPlaybackControlCoordinator> _logger;
    private readonly object _sync = new();
    private int? _savedVolume;
    private bool _isDuckActive;

    public SpotifyPlaybackControlCoordinator(
        ISpotifyClient spotifyClient,
        IOptions<SpotifyVolumeDuckOptions>? duckOptions,
        ILogger<SpotifyPlaybackControlCoordinator> logger)
    {
        _spotifyClient = spotifyClient;
        _duckOptions = duckOptions?.Value ?? new SpotifyVolumeDuckOptions();
        _logger = logger;
    }

    public async Task TryPauseAsync(string? eventKeyForLog, CancellationToken cancellationToken = default)
    {
        if (!await _spotifyClient.IsAuthenticatedAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogWarning("Spotify is not connected.");
            return;
        }

        var playback = await _spotifyClient.GetCurrentPlaybackAsync(cancellationToken).ConfigureAwait(false);
        if (playback is null)
        {
            _logger.LogWarning(
                "Event {EventKey} matched pause, but Spotify has no active playback device.",
                eventKeyForLog ?? "(scenario)");
            return;
        }

        if (!playback.IsPlaying)
        {
            _logger.LogDebug(
                "Event {EventKey} matched pause, but Spotify is already paused.",
                eventKeyForLog ?? "(scenario)");
            return;
        }

        await _spotifyClient.PauseAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Playback pause for {EventKey}", eventKeyForLog ?? "(scenario)");
    }

    public async Task TryResumeAsync(string? eventKeyForLog, CancellationToken cancellationToken = default)
    {
        if (!await _spotifyClient.IsAuthenticatedAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogWarning("Spotify is not connected.");
            return;
        }

        var playback = await _spotifyClient.GetCurrentPlaybackAsync(cancellationToken).ConfigureAwait(false);
        if (playback is null)
        {
            _logger.LogWarning(
                "Event {EventKey} matched resume, but Spotify has no active playback device.",
                eventKeyForLog ?? "(scenario)");
            return;
        }

        if (playback.IsPlaying)
        {
            _logger.LogDebug(
                "Event {EventKey} matched resume, but Spotify is already playing.",
                eventKeyForLog ?? "(scenario)");
            return;
        }

        await _spotifyClient.ResumeAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Playback resume for {EventKey}", eventKeyForLog ?? "(scenario)");
    }

    public Task TryDuckAsync(
        EventControlRule rule,
        NormalizedEvent context,
        CancellationToken cancellationToken = default)
    {
        var target = rule.VolumePercent ?? _duckOptions.MuteVolume;
        return DuckInternalAsync(target, context.EventKey, cancellationToken);
    }

    public Task TryDuckAsync(
        int volumePercent,
        string? eventKeyForLog,
        CancellationToken cancellationToken = default)
    {
        return DuckInternalAsync(volumePercent, eventKeyForLog, cancellationToken);
    }

    public async Task TryRestoreVolumeAsync(string? eventKeyForLog, CancellationToken cancellationToken = default)
    {
        if (!await _spotifyClient.IsAuthenticatedAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogWarning("Spotify is not connected.");
            return;
        }

        int restoreVolume;

        lock (_sync)
        {
            if (!_isDuckActive)
            {
                _logger.LogDebug(
                    "Event {EventKey} matched restore_volume, but no managed duck state is active.",
                    eventKeyForLog ?? "(scenario)");
                return;
            }

            restoreVolume = _savedVolume ?? _duckOptions.FallbackRestoreVolume;
            _savedVolume = null;
            _isDuckActive = false;
        }

        await _spotifyClient.SetVolumeAsync(restoreVolume, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Playback restore for {EventKey} -> volume={RestoreVolume}",
            eventKeyForLog ?? "(scenario)",
            restoreVolume);
    }

    public async Task TrySetManagedVolumeAsync(
        int volumePercent,
        string? eventKeyForLog,
        CancellationToken cancellationToken = default)
    {
        if (volumePercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(volumePercent), "Volume must be between 0 and 100.");
        }

        if (!await _spotifyClient.IsAuthenticatedAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogWarning("Spotify is not connected.");
            return;
        }

        var playback = await _spotifyClient.GetCurrentPlaybackAsync(cancellationToken).ConfigureAwait(false);
        if (playback is null)
        {
            _logger.LogWarning(
                "Managed volume for {EventKey} skipped: no active playback device.",
                eventKeyForLog ?? "(scenario)");
            return;
        }

        var restoreVolume = playback.VolumePercent ?? _duckOptions.FallbackRestoreVolume;

        lock (_sync)
        {
            if (!_isDuckActive)
            {
                _savedVolume = restoreVolume;
            }

            _isDuckActive = true;
        }

        await _spotifyClient.SetVolumeAsync(volumePercent, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug(
            "Managed volume for {EventKey} -> {Volume}% (saved restore={Saved})",
            eventKeyForLog ?? "(scenario)",
            volumePercent,
            restoreVolume);
    }

    private async Task DuckInternalAsync(
        int targetVolume,
        string? eventKeyForLog,
        CancellationToken cancellationToken)
    {
        if (!await _spotifyClient.IsAuthenticatedAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogWarning("Spotify is not connected.");
            return;
        }

        var playback = await _spotifyClient.GetCurrentPlaybackAsync(cancellationToken).ConfigureAwait(false);
        if (playback is null)
        {
            _logger.LogWarning(
                "Event {EventKey} matched duck, but Spotify has no active playback device.",
                eventKeyForLog ?? "(scenario)");
            return;
        }

        var restoreVolume = playback.VolumePercent ?? _duckOptions.FallbackRestoreVolume;

        lock (_sync)
        {
            if (!_isDuckActive)
            {
                _savedVolume = restoreVolume;
            }

            _isDuckActive = true;
        }

        await _spotifyClient.SetVolumeAsync(targetVolume, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Playback duck for {EventKey} -> volume={TargetVolume} (saved={SavedVolume})",
            eventKeyForLog ?? "(scenario)",
            targetVolume,
            restoreVolume);
    }
}
