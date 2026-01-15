using System;
using Core.Models;
using Core.Rules;
using GsiHost.Dtos;
using GsiHost.Mapping;
using Microsoft.Extensions.Logging;

namespace GsiHost.Services;

public sealed class GsiProcessingService
{
    private readonly GsiSnapshotMapper _mapper;
    private readonly IRulesEngine _rulesEngine;
    private readonly ILogger<GsiProcessingService> _logger;

    public GsiProcessingService(
        GsiSnapshotMapper mapper,
        IRulesEngine rulesEngine,
        ILogger<GsiProcessingService> logger)
    {
        _mapper = mapper;
        _rulesEngine = rulesEngine;
        _logger = logger;
    }

    public event EventHandler<GsiProcessedEventArgs>? Processed;

    public IReadOnlyList<NormalizedEvent> Process(GsiPayloadDto payload)
    {
        var snapshot = _mapper.Map(payload, DateTimeOffset.UtcNow);
        var events = _rulesEngine.Evaluate(snapshot);

        if (events.Count > 0)
        {
            _logger.LogInformation("Detected {EventCount} events", events.Count);
        }

        Processed?.Invoke(this, new GsiProcessedEventArgs(snapshot, events));
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
}
