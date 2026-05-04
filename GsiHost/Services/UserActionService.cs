using Core.Actions.Spotify;
using Core.Configuration;
using Core.Models;
using Core.Spotify;
using GsiHost.Configuration;
using GsiHost.Dtos;
using GsiHost.Tooling.Timeline;
using Microsoft.Extensions.Options;

namespace GsiHost.Services;

public sealed class UserActionService
{
    private readonly IControlProfileService _controlProfileService;
    private readonly ISpotifyPlaybackControl _playback;
    private readonly SpotifyVolumeDuckOptions _duckOptions;
    private readonly ManualMusicActionOptions _options;
    private readonly TimelineCaptureService _timeline;
    private readonly ILogger<UserActionService> _logger;

    public UserActionService(
        IControlProfileService controlProfileService,
        ISpotifyPlaybackControl playback,
        IOptions<SpotifyVolumeDuckOptions> duckOptions,
        IOptions<ManualMusicActionOptions> options,
        TimelineCaptureService timeline,
        ILogger<UserActionService> logger)
    {
        _controlProfileService = controlProfileService;
        _playback = playback;
        _duckOptions = duckOptions.Value;
        _options = options.Value;
        _timeline = timeline;
        _logger = logger;
    }

    public async Task<UserActionResponse> RecordAsync(
        UserActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            var disabled = new TimelineCommandOutcome(
                TimelineOutcomeStatuses.Disabled,
                Message: "Manual music actions are disabled.");
            var disabledEntry = _timeline.RecordUserAction(
                request.EventKey,
                request.Action,
                request.Detail,
                disabled);

            return new UserActionResponse(disabledEntry, disabled);
        }

        var eventKey = EventKeys.Normalize(request.EventKey);
        if (string.IsNullOrWhiteSpace(eventKey))
        {
            var invalid = new TimelineCommandOutcome(
                TimelineOutcomeStatuses.Invalid,
                Message: "Event key is required.");
            var invalidEntry = _timeline.RecordUserAction(
                string.Empty,
                request.Action,
                request.Detail,
                invalid);

            return new UserActionResponse(invalidEntry, invalid);
        }

        if (!eventKey.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
        {
            var invalid = new TimelineCommandOutcome(
                TimelineOutcomeStatuses.Invalid,
                Message: "Manual actions are restricted to the 'custom:' namespace.");
            var invalidEntry = _timeline.RecordUserAction(
                eventKey,
                request.Action,
                request.Detail,
                invalid);

            return new UserActionResponse(invalidEntry, invalid);
        }

        if (!IsAllowed(eventKey))
        {
            var invalid = new TimelineCommandOutcome(
                TimelineOutcomeStatuses.Invalid,
                Message: $"Manual action '{eventKey}' is not allowed by configuration.");
            var invalidEntry = _timeline.RecordUserAction(
                eventKey,
                request.Action,
                request.Detail,
                invalid);

            return new UserActionResponse(invalidEntry, invalid);
        }

        try
        {
            var rule = await ResolveRuleAsync(eventKey, cancellationToken).ConfigureAwait(false);
            if (rule is null)
            {
                var noRule = new TimelineCommandOutcome(
                    TimelineOutcomeStatuses.NoMatchingRule,
                    Message: $"No control-profile rule matched '{eventKey}'.");
                var noRuleEntry = _timeline.RecordUserAction(
                    eventKey,
                    ResolveAction(request.Action, rule),
                    request.Detail,
                    noRule);

                return new UserActionResponse(noRuleEntry, noRule);
            }

            var action = ResolveAction(request.Action, rule);
            await ApplyRuleAsync(rule, eventKey, cancellationToken).ConfigureAwait(false);

            var applied = new TimelineCommandOutcome(
                TimelineOutcomeStatuses.Applied,
                Command: rule.Command,
                Message: "Control-profile command dispatched.");
            var entry = _timeline.RecordUserAction(eventKey, action, request.Detail, applied);

            return new UserActionResponse(entry, applied);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual music action failed for {EventKey}", eventKey);
            var failed = new TimelineCommandOutcome(
                TimelineOutcomeStatuses.Failed,
                Message: "Manual music action failed.");
            var failedEntry = _timeline.RecordUserAction(eventKey, request.Action, request.Detail, failed);

            return new UserActionResponse(failedEntry, failed);
        }
    }

    private bool IsAllowed(string eventKey)
    {
        if (_options.AllowedEventKeys.Count == 0)
        {
            return true;
        }

        return _options.AllowedEventKeys.Any(allowed =>
            string.Equals(EventKeys.Normalize(allowed), eventKey, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<EventControlRule?> ResolveRuleAsync(
        string eventKey,
        CancellationToken cancellationToken)
    {
        var profilesConfig = await _controlProfileService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (profilesConfig.Profiles.Count == 0)
        {
            return null;
        }

        var activeProfile = !string.IsNullOrWhiteSpace(profilesConfig.ActiveProfileId)
            ? profilesConfig.Profiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, profilesConfig.ActiveProfileId, StringComparison.OrdinalIgnoreCase))
            : null;

        activeProfile ??= profilesConfig.Profiles.FirstOrDefault();
        return activeProfile?.FindRule(eventKey);
    }

    private async Task ApplyRuleAsync(
        EventControlRule rule,
        string eventKey,
        CancellationToken cancellationToken)
    {
        switch (rule.Command)
        {
            case MusicControlCommands.Pause:
                await _playback.TryPauseAsync(eventKey, cancellationToken).ConfigureAwait(false);
                break;

            case MusicControlCommands.Resume:
                await _playback.TryResumeAsync(eventKey, cancellationToken).ConfigureAwait(false);
                break;

            case MusicControlCommands.Duck:
                await _playback.TryDuckAsync(
                    rule.VolumePercent ?? _duckOptions.MuteVolume,
                    eventKey,
                    cancellationToken).ConfigureAwait(false);
                break;

            case MusicControlCommands.RestoreVolume:
                await _playback.TryRestoreVolumeAsync(eventKey, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private static string? ResolveAction(string? requestedAction, EventControlRule? rule)
    {
        if (!string.IsNullOrWhiteSpace(requestedAction))
        {
            return requestedAction.Trim().ToLowerInvariant();
        }

        return rule?.Command;
    }
}
