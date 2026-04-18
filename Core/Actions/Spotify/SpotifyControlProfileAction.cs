using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Configuration;
using Core.Models;
using Core.Spotify;
using Microsoft.Extensions.Logging;

namespace Core.Actions.Spotify;

public sealed class SpotifyControlProfileAction : IEventAction
{
    private readonly ISpotifyPlaybackControl _playback;
    private readonly IControlProfileService _controlProfileService;
    private readonly ILogger<SpotifyControlProfileAction> _logger;

    public SpotifyControlProfileAction(
        ISpotifyPlaybackControl playback,
        IControlProfileService controlProfileService,
        ILogger<SpotifyControlProfileAction> logger)
    {
        _playback = playback;
        _controlProfileService = controlProfileService;
        _logger = logger;
    }

    public string Key => "spotify.control_profile";

    public async Task ExecuteAsync(NormalizedEvent normalizedEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var rule = await ResolveRuleAsync(normalizedEvent.EventKey, cancellationToken).ConfigureAwait(false);
            if (rule is null)
            {
                return;
            }

            switch (rule.Command)
            {
                case MusicControlCommands.Pause:
                    await _playback.TryPauseAsync(normalizedEvent.EventKey, cancellationToken).ConfigureAwait(false);
                    break;

                case MusicControlCommands.Resume:
                    await _playback.TryResumeAsync(normalizedEvent.EventKey, cancellationToken).ConfigureAwait(false);
                    break;

                case MusicControlCommands.Duck:
                    await _playback.TryDuckAsync(rule, normalizedEvent, cancellationToken).ConfigureAwait(false);
                    break;

                case MusicControlCommands.RestoreVolume:
                    await _playback.TryRestoreVolumeAsync(normalizedEvent.EventKey, cancellationToken).ConfigureAwait(false);
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
