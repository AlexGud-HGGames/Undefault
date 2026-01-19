using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Core.Configuration;
using Core.Models;
using Core.Services;
using UI.Services;

namespace UI.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly IConfigurationService _configurationService;
    private readonly IAppStateService _appStateService;
    private readonly ISpotifyAuthService _spotifyAuthService;
    private readonly DelegateCommand _saveCommand;
    private readonly DelegateCommand _reloadCommand;
    private AppConfig? _currentConfig;
    private Dictionary<EventType, int> _eventVolumeMap = new();
    private int? _defaultVolume;
    private string _gsiEndpoint = string.Empty;
    private string _spotifyAuthStatus = "Disconnected";
    private string _spotifyPlaybackState = "N/A";
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isLoading;
    private bool _isSaving;

    public SettingsViewModel(
        IConfigurationService configurationService,
        IAppStateService appStateService,
        ISpotifyAuthService spotifyAuthService)
    {
        _configurationService = configurationService;
        _appStateService = appStateService;
        _spotifyAuthService = spotifyAuthService;

        EventSettings = new ObservableCollection<EventSettingsViewModel>(
            Enum.GetValues<EventType>().Select(type => new EventSettingsViewModel(type)));

        DefaultPlaylistUris = new ObservableCollection<EditableStringItem>();

        _saveCommand = new DelegateCommand(() => _ = SaveAsync(), () => !IsSaving);
        _reloadCommand = new DelegateCommand(() => _ = LoadAsync(), () => !IsLoading);
        AddDefaultPlaylistCommand = new DelegateCommand(() => DefaultPlaylistUris.Add(CreateItem(string.Empty, DefaultPlaylistUris)));
        CopyEndpointCommand = new DelegateCommand(() => _ = CopyEndpointAsync());
        OpenEndpointCommand = new DelegateCommand(OpenEndpoint);
        ConnectSpotifyCommand = new DelegateCommand(() => _ = ConnectSpotifyAsync());

        SubscribeToStatus();
        _ = LoadAsync();
    }

    public ObservableCollection<EventSettingsViewModel> EventSettings { get; }

    public ObservableCollection<EditableStringItem> DefaultPlaylistUris { get; }

    public string GsiEndpoint
    {
        get => _gsiEndpoint;
        set => SetField(ref _gsiEndpoint, value);
    }

    public string SpotifyAuthStatus
    {
        get => _spotifyAuthStatus;
        set => SetField(ref _spotifyAuthStatus, value);
    }

    public string SpotifyPlaybackState
    {
        get => _spotifyPlaybackState;
        set => SetField(ref _spotifyPlaybackState, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetField(ref _errorMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetField(ref _isLoading, value))
            {
                _reloadCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsSaving
    {
        get => _isSaving;
        set
        {
            if (SetField(ref _isSaving, value))
            {
                _saveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string AvailableActionKeysHint =>
        "Actions: log, spotify.pause, spotify.play, spotify.resume, spotify.volume.*";

    public ICommand SaveCommand => _saveCommand;

    public ICommand ReloadCommand => _reloadCommand;

    public ICommand AddDefaultPlaylistCommand { get; }

    public ICommand CopyEndpointCommand { get; }

    public ICommand OpenEndpointCommand { get; }

    public ICommand ConnectSpotifyCommand { get; }

    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        StatusMessage = "Loading configuration...";

        try
        {
            var config = await _configurationService.GetAsync().ConfigureAwait(false);
            _currentConfig = config;
            _eventVolumeMap = config.SpotifyActions.EventVolumeMap;
            _defaultVolume = config.SpotifyActions.DefaultVolume;

            Dispatcher.UIThread.Post(() =>
            {
                GsiEndpoint = BuildEndpointDisplay(config.GsiEndpoint);

                foreach (var eventSettings in EventSettings)
                {
                    var actionKeys = config.RulesEngine.ActionMap.TryGetValue(eventSettings.EventType, out var list)
                        ? list
                        : new List<string>();

                    var playlistUris = config.SpotifyActions.EventPlaylistMap.TryGetValue(eventSettings.EventType, out var uris)
                        ? uris
                        : new List<string>();

                    eventSettings.SetActionKeys(actionKeys);
                    eventSettings.SetPlaylistUris(playlistUris);
                }

                DefaultPlaylistUris.Clear();
                foreach (var uri in config.SpotifyActions.DefaultPlaylistUris)
                {
                    DefaultPlaylistUris.Add(CreateItem(uri, DefaultPlaylistUris));
                }

                StatusMessage = "Configuration loaded.";
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ErrorMessage = $"Failed to load configuration: {ex.Message}";
                StatusMessage = string.Empty;
            });
        }
        finally
        {
            Dispatcher.UIThread.Post(() => IsLoading = false);
        }
    }

    private async Task SaveAsync()
    {
        if (_currentConfig is null)
        {
            return;
        }

        IsSaving = true;
        ErrorMessage = string.Empty;
        StatusMessage = "Saving configuration...";

        try
        {
            var actionMap = EventSettings.ToDictionary(
                item => item.EventType,
                item => item.GetActionKeys());

            var playlistMap = EventSettings.ToDictionary(
                item => item.EventType,
                item => item.GetPlaylistUris());

            var defaultPlaylists = DefaultPlaylistUris
                .Select(item => item.Value.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();

            var spotifyActions = new SpotifyActionsConfig(
                playlistMap,
                _eventVolumeMap,
                defaultPlaylists,
                _defaultVolume
            );

            var updatedConfig = new AppConfig(
                new RulesEngineConfig(actionMap),
                _currentConfig.Spotify,
                spotifyActions,
                _currentConfig.GsiEndpoint
            );

            await _configurationService.SaveAsync(updatedConfig).ConfigureAwait(false);

            Dispatcher.UIThread.Post(() => StatusMessage = "Configuration saved.");
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ErrorMessage = $"Failed to save configuration: {ex.Message}";
                StatusMessage = string.Empty;
            });
        }
        finally
        {
            Dispatcher.UIThread.Post(() => IsSaving = false);
        }
    }

    private async Task CopyEndpointAsync()
    {
        var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var clipboard = lifetime?.MainWindow?.Clipboard;
        if (clipboard is null || string.IsNullOrWhiteSpace(GsiEndpoint))
        {
            return;
        }

        await clipboard.SetTextAsync(GsiEndpoint);
        StatusMessage = "Endpoint copied to clipboard.";
    }

    private void OpenEndpoint()
    {
        if (string.IsNullOrWhiteSpace(GsiEndpoint))
        {
            return;
        }

        var url = ExtractUrl(GsiEndpoint);
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private async Task ConnectSpotifyAsync()
    {
        try
        {
            var url = await _spotifyAuthService.GetAuthorizationUrlAsync().ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(url))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() => ErrorMessage = $"Spotify auth failed: {ex.Message}");
        }
    }

    private void SubscribeToStatus()
    {
        _appStateService.StatusSnapshot.Subscribe(status =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                SpotifyAuthStatus = status.SpotifyStatus;
                SpotifyPlaybackState = status.PlaybackState;
            });
        });
    }

    private static EditableStringItem CreateItem(string value, ObservableCollection<EditableStringItem> collection)
    {
        return new EditableStringItem(value, item => collection.Remove(item));
    }

    private static string BuildEndpointDisplay(GsiEndpointInfo endpointInfo)
    {
        var baseUrl = endpointInfo.Url ?? string.Empty;
        var normalizedBase = baseUrl.TrimEnd('/');
        var normalizedPath = endpointInfo.Path.StartsWith('/')
            ? endpointInfo.Path
            : "/" + endpointInfo.Path;

        var fullUrl = string.IsNullOrWhiteSpace(normalizedBase)
            ? normalizedPath
            : $"{normalizedBase}{normalizedPath}";

        return $"{endpointInfo.Method} {fullUrl}";
    }

    private static string ExtractUrl(string display)
    {
        var parts = display.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? parts[1] : display;
    }
}
