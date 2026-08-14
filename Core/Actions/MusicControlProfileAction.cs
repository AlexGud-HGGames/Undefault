using Core.Configuration;
using Core.Models;
using Core.Music;
using Microsoft.Extensions.Logging;

namespace Core.Actions;

/// <summary>
/// Applies the active console control profile through <see cref="IMusicPlaybackControl"/>.
/// </summary>
public sealed class MusicControlProfileAction : IEventAction
{
    /// <summary>
    /// Canonical ActionMap key after the Tauon pivot.
    /// </summary>
    public const string CanonicalKey = "music.control_profile";

    /// <summary>
    /// Compatibility ActionMap key used during migration.
    /// </summary>
    public const string LegacySpotifyKey = "spotify.control_profile";

    private readonly IMusicPlaybackControl _playback;
    private readonly IControlProfileService _controlProfileService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicControlProfileAction"/> class.
    /// </summary>
    /// <param name="playback">The session playback coordinator.</param>
    /// <param name="controlProfileService">The control-profile store.</param>
    /// <param name="logger">The logger used when a command fails.</param>
    /// <param name="actionKey">The ActionMap key this instance registers under.</param>
    public MusicControlProfileAction(
        IMusicPlaybackControl playback,
        IControlProfileService controlProfileService,
        ILogger<MusicControlProfileAction> logger,
        string actionKey = CanonicalKey)
        : this(playback, controlProfileService, (ILogger)logger, actionKey)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicControlProfileAction"/> class.
    /// </summary>
    /// <param name="playback">The session playback coordinator.</param>
    /// <param name="controlProfileService">The control-profile store.</param>
    /// <param name="logger">The logger used when a command fails.</param>
    /// <param name="actionKey">The ActionMap key this instance registers under.</param>
    public MusicControlProfileAction(
        IMusicPlaybackControl playback,
        IControlProfileService controlProfileService,
        ILogger logger,
        string actionKey = CanonicalKey)
    {
        _playback = playback;
        _controlProfileService = controlProfileService;
        _logger = logger;
        Key = string.IsNullOrWhiteSpace(actionKey) ? CanonicalKey : actionKey;
    }

    /// <inheritdoc />
    public string Key { get; }

    /// <inheritdoc />
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

                case MusicControlCommands.Next:
                    await _playback.TryNextAsync(normalizedEvent.EventKey, cancellationToken).ConfigureAwait(false);
                    break;

                case MusicControlCommands.Previous:
                    await _playback.TryPreviousAsync(normalizedEvent.EventKey, cancellationToken).ConfigureAwait(false);
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
