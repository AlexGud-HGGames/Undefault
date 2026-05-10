using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Adapters;
using Core.Models;
using Core.Music;
using Core.Rules;
using GsiHost.Configuration;
using GsiHost.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GsiHost.Services;

public sealed class GsiProcessingService
{
    private readonly IGameAdapter<GsiPayloadDto> _adapter;
    private readonly IRulesEngine _rulesEngine;
    private readonly RuntimeOptions _runtime;
    private readonly IMusicOrchestrationFacade _musicFacade;
    private readonly IShadowMusicSnapshotSink _shadowSink;
    private readonly MusicOrchestrationOptions _musicOrchestration;
    private readonly ILogger<GsiProcessingService> _logger;
    private int _hasLoggedConnection;

    public GsiProcessingService(
        IGameAdapter<GsiPayloadDto> adapter,
        IRulesEngine rulesEngine,
        IOptions<RuntimeOptions> runtime,
        IMusicOrchestrationFacade musicFacade,
        IShadowMusicSnapshotSink shadowSink,
        IOptions<MusicOrchestrationOptions> musicOrchestration,
        ILogger<GsiProcessingService> logger)
    {
        _adapter = adapter;
        _rulesEngine = rulesEngine;
        _runtime = runtime.Value;
        _musicFacade = musicFacade;
        _shadowSink = shadowSink;
        _musicOrchestration = musicOrchestration.Value;
        _logger = logger;
    }

    public event EventHandler<GsiProcessedEventArgs>? Processed;

    public async Task<IReadOnlyList<NormalizedEvent>> ProcessAsync(
        GsiPayloadDto payload,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _hasLoggedConnection, 1) == 0)
        {
            _logger.LogInformation("CS2 GSI connected.");
        }

        var observation = _adapter.Adapt(payload, DateTimeOffset.UtcNow);
        var events = _runtime.IsIntentCapture
            ? await _rulesEngine.DetectAsync(observation, cancellationToken).ConfigureAwait(false)
            : await _rulesEngine.EvaluateAsync(observation, cancellationToken).ConfigureAwait(false);

        // Shadow facade runs after the legacy path and must never break it; failures are
        // swallowed with a warning.
        if (_musicOrchestration.ShadowMode)
        {
            try
            {
                var snapshot = _musicFacade.EvaluateShadow(observation);
                _shadowSink.Record(snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Music orchestration shadow evaluation failed; legacy path unaffected.");
            }
        }

        Processed?.Invoke(this, new GsiProcessedEventArgs(observation.Raw, events)
        {
            Observation = observation
        });
        return events;
    }
}

public sealed class GsiProcessedEventArgs : EventArgs
{
    public GsiProcessedEventArgs(GameSnapshot snapshot, IReadOnlyList<NormalizedEvent> events)
    {
        Snapshot = snapshot;
        Events = events;
    }

    public GameSnapshot Snapshot { get; }

    public IReadOnlyList<NormalizedEvent> Events { get; }

    internal AdapterObservation? Observation { get; init; }
}
