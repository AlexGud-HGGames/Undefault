using Core.Actions.Spotify;
using Core.Configuration;
using Core.Models;
using Core.Spotify;
using GsiHost.Configuration;
using GsiHost.Dtos;
using GsiHost.Models;
using Microsoft.Extensions.Options;

namespace GsiHost.Services;

public sealed class UserActionService
{
    private readonly ISpotifyPlaybackControl _playback;
    private readonly SpotifyVolumeDuckOptions _duckOptions;
    private readonly ManualMusicActionOptions _options;
    private readonly RuntimeOptions _runtime;
    private readonly TimelineCaptureService _timeline;
    private readonly ILogger<UserActionService> _logger;

    public UserActionService(
        ISpotifyPlaybackControl playback,
        IOptions<SpotifyVolumeDuckOptions> duckOptions,
        IOptions<ManualMusicActionOptions> options,
        IOptions<RuntimeOptions> runtime,
        TimelineCaptureService timeline,
        ILogger<UserActionService> logger)
    {
        _playback = playback;
        _duckOptions = duckOptions.Value;
        _options = options.Value;
        _runtime = runtime.Value;
        _timeline = timeline;
        _logger = logger;
    }

    public async Task<UserActionResponse> RecordAsync(
        UserActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_runtime.IsIntentCapture)
        {
            var disabled = new TimelineCommandOutcome(
                TimelineOutcomeStatuses.Disabled,
                Message: "Manual music actions are only available in intent_capture runtime mode.");
            var disabledEntry = _timeline.RecordUserAction(
                request.EventKey,
                request.Action,
                request.Detail,
                disabled);

            return new UserActionResponse(disabledEntry, disabled);
        }

        if (!_options.IsEnabled(_runtime))
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
            var mapping = ResolveMapping(eventKey);
            if (mapping is null)
            {
                var noRule = new TimelineCommandOutcome(
                    TimelineOutcomeStatuses.NoMatchingRule,
                    Message: $"No manual action command mapping matched '{eventKey}'.");
                var noRuleEntry = _timeline.RecordUserAction(
                    eventKey,
                    ResolveAction(request.Action, mapping),
                    request.Detail,
                    noRule);

                return new UserActionResponse(noRuleEntry, noRule);
            }

            var action = ResolveAction(request.Action, mapping);
            await ApplyMappingAsync(mapping, eventKey, cancellationToken).ConfigureAwait(false);

            var applied = new TimelineCommandOutcome(
                TimelineOutcomeStatuses.Applied,
                Command: mapping.Command,
                Message: "Manual action command dispatched.");
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

    private ManualMusicActionMappingOptions? ResolveMapping(string eventKey)
    {
        return _options.GetCommandMappings()
            .Select(NormalizeMapping)
            .FirstOrDefault(mapping =>
                string.Equals(mapping.EventKey, eventKey, StringComparison.OrdinalIgnoreCase)
                && MusicControlCommands.IsSupported(mapping.Command));
    }

    private async Task ApplyMappingAsync(
        ManualMusicActionMappingOptions mapping,
        string eventKey,
        CancellationToken cancellationToken)
    {
        switch (mapping.Command)
        {
            case MusicControlCommands.Pause:
                await _playback.TryPauseAsync(eventKey, cancellationToken).ConfigureAwait(false);
                break;

            case MusicControlCommands.Resume:
                await _playback.TryResumeAsync(eventKey, cancellationToken).ConfigureAwait(false);
                break;

            case MusicControlCommands.Duck:
                await _playback.TryDuckAsync(
                    mapping.VolumePercent ?? _duckOptions.MuteVolume,
                    eventKey,
                    cancellationToken).ConfigureAwait(false);
                break;

            case MusicControlCommands.RestoreVolume:
                await _playback.TryRestoreVolumeAsync(eventKey, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private static ManualMusicActionMappingOptions NormalizeMapping(ManualMusicActionMappingOptions mapping)
    {
        var command = MusicControlCommands.Normalize(mapping.Command);
        var volumePercent = command == MusicControlCommands.Duck
            ? mapping.VolumePercent
            : null;

        return mapping with
        {
            EventKey = EventKeys.Normalize(mapping.EventKey),
            Command = command,
            VolumePercent = volumePercent
        };
    }

    private static string? ResolveAction(string? requestedAction, ManualMusicActionMappingOptions? mapping)
    {
        if (!string.IsNullOrWhiteSpace(requestedAction))
        {
            return requestedAction.Trim().ToLowerInvariant();
        }

        return mapping?.Command;
    }
}
