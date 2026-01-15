using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Models;
using Core.Services;
using Core.Spotify;
using Microsoft.Extensions.Logging;

namespace GsiHost.Services;

public sealed class AppStateService : IAppStateService, IDisposable
{
    private const int MaxEvents = 200;
    private readonly object _lock = new();
    private readonly ISpotifyClient _spotifyClient;
    private readonly ILogger<AppStateService> _logger;
    private readonly GsiProcessingService _processor;
    private readonly SimpleSubject<StatusSnapshot> _statusSubject = new();
    private readonly SimpleSubject<NormalizedEvent> _eventSubject = new();
    private readonly List<NormalizedEvent> _recentEvents = new();
    private StatusSnapshot _current;

    public AppStateService(GsiProcessingService processor, ISpotifyClient spotifyClient, ILogger<AppStateService> logger)
    {
        _processor = processor;
        _spotifyClient = spotifyClient;
        _logger = logger;
        _current = new StatusSnapshot(
            GsiStatus: "Disconnected",
            LastSnapshotAt: null,
            Game: "Unknown",
            LastEvent: null,
            SpotifyStatus: "Disconnected",
            PlaybackState: "Stopped"
        );

        _processor.Processed += OnProcessed;
    }

    public IObservable<StatusSnapshot> StatusSnapshot => _statusSubject;

    public IObservable<NormalizedEvent> Events => _eventSubject;

    public Task<StatusSnapshot> GetCurrentStatusAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_current);
        }
    }

    public IReadOnlyList<NormalizedEvent> GetRecentEvents()
    {
        lock (_lock)
        {
            return _recentEvents.ToList();
        }
    }

    private void OnProcessed(object? sender, GsiProcessedEventArgs args)
    {
        _ = UpdateAsync(args);
    }

    private async Task UpdateAsync(GsiProcessedEventArgs args)
    {
        var snapshot = args.Snapshot;
        var lastEvent = args.Events.LastOrDefault();
        var gsiStatus = "Connected";
        var game = snapshot.GameId ?? "Unknown";

        string spotifyStatus;
        string playbackState;

        try
        {
            spotifyStatus = await _spotifyClient.IsAuthenticatedAsync().ConfigureAwait(false)
                ? "Connected"
                : "Disconnected";

            var playback = await _spotifyClient.GetCurrentPlaybackAsync().ConfigureAwait(false);
            playbackState = playback is null
                ? "Stopped"
                : playback.IsPlaying
                    ? "Playing"
                    : "Paused";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Spotify status.");
            spotifyStatus = "Unknown";
            playbackState = "Unknown";
        }

        var status = new StatusSnapshot(
            GsiStatus: gsiStatus,
            LastSnapshotAt: snapshot.Timestamp,
            Game: game,
            LastEvent: lastEvent,
            SpotifyStatus: spotifyStatus,
            PlaybackState: playbackState
        );

        lock (_lock)
        {
            _current = status;

            foreach (var normalizedEvent in args.Events)
            {
                _recentEvents.Add(normalizedEvent);
                if (_recentEvents.Count > MaxEvents)
                {
                    _recentEvents.RemoveAt(0);
                }
            }
        }

        foreach (var normalizedEvent in args.Events)
        {
            _eventSubject.OnNext(normalizedEvent);
        }

        _statusSubject.OnNext(status);
    }

    public void Dispose()
    {
        _processor.Processed -= OnProcessed;
    }
}
