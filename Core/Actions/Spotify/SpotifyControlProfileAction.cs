using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Configuration;
using Core.Models;
using Core.Spotify;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Actions.Spotify;

public sealed class SpotifyControlProfileAction : IEventAction
{
    private readonly ISpotifyClient _spotifyClient;
    private readonly IControlProfileService _controlProfileService;
    private readonly SpotifyVolumeDuckOptions _duckOptions;
    private readonly ILogger<SpotifyControlProfileAction> _logger;
    private readonly object _sync = new();
    private int? _savedVolume;
    private bool _isDuckActive;

    public SpotifyControlProfileAction(
        ISpotifyClient spotifyClient,
        IControlProfileService controlProfileService,
        IOptions<SpotifyVolumeDuckOptions>? duckOptions,
        ILogger<SpotifyControlProfileAction> logger)
    {
        _spotifyClient = spotifyClient;
        _controlProfileService = controlProfileService;
        _duckOptions = duckOptions?.Value ?? new SpotifyVolumeDuckOptions();
        _logger = logger;
    }

    public string Key => "spotify.control_profile";

    public async Task ExecuteAsync(NormalizedEvent normalizedEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await _spotifyClient.IsAuthenticatedAsync(cancellationToken).ConfigureAwait(false))
            {
                _logger.LogWarning("Spotify is not connected.");
                return;
            }

            var rule = await ResolveRuleAsync(normalizedEvent.EventKey, cancellationToken).ConfigureAwait(false);
            if (rule is null)
            {
                return;
            }

            switch (rule.Command)
            {
                case MusicControlCommands.Pause:
                    await PauseAsync(normalizedEvent, cancellationToken).ConfigureAwait(false);
                    break;

                case MusicControlCommands.Resume:
                    await ResumeAsync(normalizedEvent, cancellationToken).ConfigureAwait(false);
                    break;

                case MusicControlCommands.Duck:
                    await DuckAsync(rule, normalizedEvent, cancellationToken).ConfigureAwait(false);
                    break;

                case MusicControlCommands.RestoreVolume:
                    await RestoreVolumeAsync(normalizedEvent, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Control profile action failed for {EventKey}", normalizedEvent.EventKey);
        }
    }

    private async Task<EventControlRule?> ResolveRuleAsync(string eventKey, CancellationToken cancellationToken)
    {
        var profilesConfig = await _controlProfileService.GetAsync(cancellationToken).ConfigureAwait(false);
        var profiles = profilesConfig.Profiles;
        if (profiles.Count == 0)
        {
            return null;
        }

        var activeProfile = ResolveActiveProfile(profiles, profilesConfig.ActiveProfileId);
        if (activeProfile is null)
        {
            return null;
        }

        return activeProfile.FindRule(eventKey);
    }

    private async Task PauseAsync(NormalizedEvent normalizedEvent, CancellationToken cancellationToken)
    {
        var playback = await _spotifyClient.GetCurrentPlaybackAsync(cancellationToken).ConfigureAwait(false);
        if (playback is null)
        {
            _logger.LogWarning("Event {EventKey} matched pause, but Spotify has no active playback device.", normalizedEvent.EventKey);
            return;
        }

        if (!playback.IsPlaying)
        {
            _logger.LogDebug("Event {EventKey} matched pause, but Spotify is already paused.", normalizedEvent.EventKey);
            return;
        }

        await _spotifyClient.PauseAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Control profile pause for event {EventKey}", normalizedEvent.EventKey);
    }

    private async Task ResumeAsync(NormalizedEvent normalizedEvent, CancellationToken cancellationToken)
    {
        var playback = await _spotifyClient.GetCurrentPlaybackAsync(cancellationToken).ConfigureAwait(false);
        if (playback is null)
        {
            _logger.LogWarning("Event {EventKey} matched resume, but Spotify has no active playback device.", normalizedEvent.EventKey);
            return;
        }

        if (playback.IsPlaying)
        {
            _logger.LogDebug("Event {EventKey} matched resume, but Spotify is already playing.", normalizedEvent.EventKey);
            return;
        }

        await _spotifyClient.ResumeAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Control profile resume for event {EventKey}", normalizedEvent.EventKey);
    }

    private async Task DuckAsync(
        EventControlRule rule,
        NormalizedEvent normalizedEvent,
        CancellationToken cancellationToken)
    {
        var playback = await _spotifyClient.GetCurrentPlaybackAsync(cancellationToken).ConfigureAwait(false);
        if (playback is null)
        {
            _logger.LogWarning("Event {EventKey} matched duck, but Spotify has no active playback device.", normalizedEvent.EventKey);
            return;
        }

        var restoreVolume = playback.VolumePercent ?? _duckOptions.FallbackRestoreVolume;
        var targetVolume = rule.VolumePercent ?? _duckOptions.MuteVolume;

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
            "Control profile duck for event {EventKey} -> volume={TargetVolume} (saved={SavedVolume})",
            normalizedEvent.EventKey,
            targetVolume,
            restoreVolume);
    }

    private async Task RestoreVolumeAsync(NormalizedEvent normalizedEvent, CancellationToken cancellationToken)
    {
        int restoreVolume;

        lock (_sync)
        {
            if (!_isDuckActive)
            {
                _logger.LogDebug("Event {EventKey} matched restore_volume, but no managed duck state is active.", normalizedEvent.EventKey);
                return;
            }

            restoreVolume = _savedVolume ?? _duckOptions.FallbackRestoreVolume;
            _savedVolume = null;
            _isDuckActive = false;
        }

        await _spotifyClient.SetVolumeAsync(restoreVolume, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Control profile restore for event {EventKey} -> volume={RestoreVolume}",
            normalizedEvent.EventKey,
            restoreVolume);
    }

    private static ConsoleControlProfile? ResolveActiveProfile(
        IReadOnlyList<ConsoleControlProfile> profiles,
        string? activeProfileId)
    {
        if (!string.IsNullOrWhiteSpace(activeProfileId))
        {
            return profiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, activeProfileId, StringComparison.OrdinalIgnoreCase));
        }

        return profiles.FirstOrDefault();
    }
}
