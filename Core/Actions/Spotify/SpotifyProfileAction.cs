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

public sealed class SpotifyProfileAction : IEventAction
{
    private readonly ISpotifyClient _spotifyClient;
    private readonly IProfileService _profileService;
    private readonly ILogger<SpotifyProfileAction> _logger;

    public SpotifyProfileAction(
        ISpotifyClient spotifyClient,
        IProfileService profileService,
        ILogger<SpotifyProfileAction> logger)
    {
        _spotifyClient = spotifyClient;
        _profileService = profileService;
        _logger = logger;
    }

    public string Key => "spotify.profile";

    public async Task ExecuteAsync(NormalizedEvent normalizedEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var isAuthenticated = await _spotifyClient.IsAuthenticatedAsync(cancellationToken);
            if (!isAuthenticated)
            {
                _logger.LogWarning("Spotify not authenticated, skipping profile action");
                return;
            }

            var rule = await ResolveRuleAsync(normalizedEvent.Type, cancellationToken);
            if (rule is null || rule.Action == EventAction.None)
            {
                return;
            }

            if (rule.Volume.HasValue)
            {
                await _spotifyClient.SetVolumeAsync(rule.Volume.Value, cancellationToken);
            }

            switch (rule.Action)
            {
                case EventAction.Play:
                    var uri = ChooseUri(rule.Tracks);
                    await _spotifyClient.PlayAsync(uri, cancellationToken);
                    _logger.LogInformation("Spotify play (profile) for event {EventType}", normalizedEvent.Type);
                    break;
                case EventAction.Pause:
                    await _spotifyClient.PauseAsync(cancellationToken);
                    _logger.LogInformation("Spotify pause (profile) for event {EventType}", normalizedEvent.Type);
                    break;
                case EventAction.Resume:
                    await _spotifyClient.ResumeAsync(cancellationToken);
                    _logger.LogInformation("Spotify resume (profile) for event {EventType}", normalizedEvent.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply Spotify profile rule for event {EventType}", normalizedEvent.Type);
        }
    }

    private async Task<EventRule?> ResolveRuleAsync(EventType eventType, CancellationToken cancellationToken)
    {
        var profilesConfig = await _profileService.GetAsync(cancellationToken);
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

        return activeProfile.Rules.TryGetValue(eventType, out var rule) ? rule : null;
    }

    private static MusicProfile? ResolveActiveProfile(
        IReadOnlyList<MusicProfile> profiles,
        string? activeProfileId)
    {
        if (!string.IsNullOrWhiteSpace(activeProfileId))
        {
            return profiles.FirstOrDefault(profile => profile.Id == activeProfileId);
        }

        return profiles.FirstOrDefault();
    }

    private static string? ChooseUri(IReadOnlyList<string> uris)
    {
        if (uris.Count == 0)
        {
            return null;
        }

        if (uris.Count == 1)
        {
            return uris[0];
        }

        return uris[Random.Shared.Next(uris.Count)];
    }
}
