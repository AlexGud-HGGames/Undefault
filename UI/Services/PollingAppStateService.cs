using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Core.Models;
using Core.Services;

namespace UI.Services;

public sealed class PollingAppStateService : IAppStateService, IDisposable
{
    private const int MaxEventKeys = 500;
    private readonly HttpClient _httpClient;
    private readonly SimpleSubject<StatusSnapshot> _statusSubject = new();
    private readonly SimpleSubject<NormalizedEvent> _eventSubject = new();
    private readonly object _eventLock = new();
    private readonly HashSet<string> _eventKeys = new(StringComparer.Ordinal);
    private readonly Queue<string> _eventOrder = new();
    private readonly CancellationTokenSource _cts = new();
    private StatusSnapshot _current = new(
        GsiStatus: "Disconnected",
        LastSnapshotAt: null,
        Game: "Unknown",
        LastEvent: null,
        SpotifyStatus: "Disconnected",
        PlaybackState: "Stopped"
    );

    public PollingAppStateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _ = PollAsync(_cts.Token);
    }

    public IObservable<StatusSnapshot> StatusSnapshot => _statusSubject;

    public IObservable<NormalizedEvent> Events => _eventSubject;

    public async Task<StatusSnapshot> GetCurrentStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = await _httpClient.GetFromJsonAsync<StatusSnapshot>("status", cancellationToken)
            .ConfigureAwait(false);

        if (status is not null)
        {
            _current = status;
        }

        return _current;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                StatusSnapshot? status = null;
                List<NormalizedEvent>? events = null;

                try
                {
                    status = await GetCurrentStatusAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    continue;
                }

                _statusSubject.OnNext(status);

                try
                {
                    events = await _httpClient.GetFromJsonAsync<List<NormalizedEvent>>("events", cancellationToken)
                        .ConfigureAwait(false);
                }
                catch
                {
                    continue;
                }

                if (events is null)
                {
                    continue;
                }

                foreach (var normalizedEvent in events)
                {
                    if (!TryRegisterEvent(normalizedEvent))
                    {
                        continue;
                    }

                    _eventSubject.OnNext(normalizedEvent);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private bool TryRegisterEvent(NormalizedEvent normalizedEvent)
    {
        var key = BuildEventKey(normalizedEvent);

        lock (_eventLock)
        {
            if (_eventKeys.Contains(key))
            {
                return false;
            }

            _eventKeys.Add(key);
            _eventOrder.Enqueue(key);

            while (_eventOrder.Count > MaxEventKeys)
            {
                var oldKey = _eventOrder.Dequeue();
                _eventKeys.Remove(oldKey);
            }

            return true;
        }
    }

    private static string BuildEventKey(NormalizedEvent normalizedEvent)
    {
        var context = normalizedEvent.Context;
        return string.Join(
            '|',
            normalizedEvent.Type,
            normalizedEvent.Timestamp.ToString("O"),
            context.GameId ?? string.Empty,
            context.MatchId ?? string.Empty,
            context.PlayerId ?? string.Empty
        );
    }
}
