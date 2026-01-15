namespace UI.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private string _gsiHost = "http://127.0.0.1:3000";
    private string _selectedGame = "CS2";
    private string _activeProfile = "Default";
    private string _spotifyAuthStatus = "Not connected";
    private bool _autoStart;
    private bool _minimizeToTray = true;

    public string GsiHost
    {
        get => _gsiHost;
        set => SetField(ref _gsiHost, value);
    }

    public string SelectedGame
    {
        get => _selectedGame;
        set => SetField(ref _selectedGame, value);
    }

    public string ActiveProfile
    {
        get => _activeProfile;
        set => SetField(ref _activeProfile, value);
    }

    public string SpotifyAuthStatus
    {
        get => _spotifyAuthStatus;
        set => SetField(ref _spotifyAuthStatus, value);
    }

    public bool AutoStart
    {
        get => _autoStart;
        set => SetField(ref _autoStart, value);
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set => SetField(ref _minimizeToTray, value);
    }
}
