using System;
using System.Threading;
using System.Threading.Tasks;
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
    private int _hasLoggedConnection;

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

    public async Task<IReadOnlyList<NormalizedEvent>> ProcessAsync(
        GsiPayloadDto payload,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _hasLoggedConnection, 1) == 0)
        {
            _logger.LogInformation("CS2 GSI connected.");
        }

        var snapshot = _mapper.Map(payload, DateTimeOffset.UtcNow);
        var events = await _rulesEngine.EvaluateAsync(snapshot, cancellationToken).ConfigureAwait(false);

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
