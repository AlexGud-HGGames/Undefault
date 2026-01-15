namespace UI.ViewModels;

public class StatusViewModel : ViewModelBase
{
    private string _gsiStatus = "Disconnected";
    private string _lastSnapshotTime = "N/A";
    private string _game = "Unknown";
    private string _lastEvent = "N/A";
    private string _spotifyStatus = "Disconnected";
    private string _playbackState = "N/A";

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
}
