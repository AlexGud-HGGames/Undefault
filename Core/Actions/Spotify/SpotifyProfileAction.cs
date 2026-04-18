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
    private readonly IPlaybackPolicy _playbackPolicy;
    private readonly ITrackPlaybackService _trackPlaybackService;
    private readonly ILogger<SpotifyProfileAction> _logger;

    public SpotifyProfileAction(
        ISpotifyClient spotifyClient,
        IProfileService profileService,
        IPlaybackPolicy playbackPolicy,
        ITrackPlaybackService trackPlaybackService,
        ILogger<SpotifyProfileAction> logger)
    {
        _spotifyClient = spotifyClient;
        _profileService = profileService;
        _playbackPolicy = playbackPolicy;
        _trackPlaybackService = trackPlaybackService;
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

            var rule = await ResolveRuleAsync(normalizedEvent.EventKey, cancellationToken);
            if (rule is null)
            {
                return;
            }

            var uri = ChooseUri(rule.Tracks);
            if (string.IsNullOrWhiteSpace(uri))
            {
                _logger.LogDebug("No Spotify URIs configured for event {EventKey}", normalizedEvent.EventKey);
                return;
            }

            await _playbackPolicy.BeforePlayAsync(normalizedEvent, cancellationToken);
            await _trackPlaybackService.PlayTrackAsync(uri, cancellationToken);
            _logger.LogInformation("Spotify play (profile) for event {EventKey}", normalizedEvent.EventKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply Spotify profile rule for event {EventKey}", normalizedEvent.EventKey);
        }
    }

    private async Task<EventTrackRule?> ResolveRuleAsync(string eventKey, CancellationToken cancellationToken)
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

        return activeProfile.FindRule(eventKey);
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
