using Avalonia.Threading;
using Core.Models;
using Core.Services;
using UI.Services;

namespace UI.ViewModels;

public class StatusViewModel : ViewModelBase
{
    private string _gsiStatus = "Disconnected";
    private string _lastSnapshotTime = "N/A";
    private string _game = "Unknown";
    private string _lastEvent = "N/A";
    private string _spotifyStatus = "Disconnected";
    private string _playbackState = "N/A";
    private bool _isLoading = true;
    private string _errorMessage = string.Empty;
    private bool _hasError;

    public StatusViewModel(IAppStateService appStateService)
    {
        appStateService.StatusSnapshot.Subscribe(status =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                GsiStatus = status.GsiStatus;
                LastSnapshotTime = status.LastSnapshotAt?.ToLocalTime().ToString("HH:mm:ss") ?? "N/A";
                Game = string.IsNullOrWhiteSpace(status.Game) ? "Unknown" : status.Game;
                LastEvent = FormatLastEvent(status.LastEvent);
                SpotifyStatus = status.SpotifyStatus;
                PlaybackState = status.PlaybackState;
                IsLoading = false;
                ErrorMessage = string.Empty;
                HasError = false;
            });
        });
    }

    public string GsiStatus
    {
        get => _gsiStatus;
        set => SetField(ref _gsiStatus, value);
    }

    public string LastSnapshotTime
    {
        get => _lastSnapshotTime;
        set => SetField(ref _lastSnapshotTime, value);
    }

    public string Game
    {
        get => _game;
        set => SetField(ref _game, value);
    }

    public string LastEvent
    {
        get => _lastEvent;
        set => SetField(ref _lastEvent, value);
    }

    public string SpotifyStatus
    {
        get => _spotifyStatus;
        set => SetField(ref _spotifyStatus, value);
    }

    public string PlaybackState
    {
        get => _playbackState;
        set => SetField(ref _playbackState, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetField(ref _isLoading, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetField(ref _errorMessage, value);
    }

    public bool HasError
    {
        get => _hasError;
        set => SetField(ref _hasError, value);
    }

    private static string FormatLastEvent(NormalizedEvent? normalizedEvent)
    {
        if (normalizedEvent is null)
        {
            return "N/A";
        }

        var timestamp = normalizedEvent.Timestamp.ToLocalTime().ToString("HH:mm:ss");
        return $"{normalizedEvent.Type} @ {timestamp}";
    }
}
