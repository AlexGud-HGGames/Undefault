using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Adapters;
using Core.Models;
using Core.Rules;
using GsiHost.Dtos;
using Microsoft.Extensions.Logging;

namespace GsiHost.Services;

public sealed class GsiProcessingService
{
    private readonly IGameAdapter<GsiPayloadDto> _adapter;
    private readonly IRulesEngine _rulesEngine;
    private readonly ILogger<GsiProcessingService> _logger;
    private int _hasLoggedConnection;

    public GsiProcessingService(
        IGameAdapter<GsiPayloadDto> adapter,
        IRulesEngine rulesEngine,
        ILogger<GsiProcessingService> logger)
    {
        _adapter = adapter;
        _rulesEngine = rulesEngine;
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
        var events = await _rulesEngine.EvaluateAsync(observation.Raw, cancellationToken).ConfigureAwait(false);

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
