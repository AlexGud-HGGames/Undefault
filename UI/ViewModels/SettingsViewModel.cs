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
    private readonly IProfileService _profileService;
    private readonly IAppStateService _appStateService;
    private readonly ISpotifyAuthService _spotifyAuthService;
    private readonly DelegateCommand _saveCommand;
    private readonly DelegateCommand _reloadCommand;
    private SystemConfig? _systemConfig;
    private MusicProfilesConfig? _profilesConfig;
    private string _activeProfileId = "default";
    private string _activeProfileName = "Default";
    private string _gsiEndpoint = string.Empty;
    private string _spotifyAuthStatus = "Disconnected";
    private string _spotifyPlaybackState = "N/A";
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isLoading;
    private bool _isSaving;

    public SettingsViewModel(
        IConfigurationService configurationService,
        IProfileService profileService,
        IAppStateService appStateService,
        ISpotifyAuthService spotifyAuthService)
    {
        _configurationService = configurationService;
        _profileService = profileService;
        _appStateService = appStateService;
        _spotifyAuthService = spotifyAuthService;

        EventSettings = new ObservableCollection<EventSettingsViewModel>(
            Enum.GetValues<EventType>().Select(type => new EventSettingsViewModel(type)));

        _saveCommand = new DelegateCommand(() => _ = SaveAsync(), () => !IsSaving);
        _reloadCommand = new DelegateCommand(() => _ = LoadAsync(), () => !IsLoading);
        CopyEndpointCommand = new DelegateCommand(() => _ = CopyEndpointAsync());
        OpenEndpointCommand = new DelegateCommand(OpenEndpoint);
        ConnectSpotifyCommand = new DelegateCommand(() => _ = ConnectSpotifyAsync());

        SubscribeToStatus();
        _ = LoadAsync();
    }

    public ObservableCollection<EventSettingsViewModel> EventSettings { get; }

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

    public ICommand SaveCommand => _saveCommand;

    public ICommand ReloadCommand => _reloadCommand;

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
            var systemConfig = await _configurationService.GetAsync().ConfigureAwait(false);
            var profilesConfig = await _profileService.GetAsync().ConfigureAwait(false);
            _systemConfig = systemConfig;
            _profilesConfig = profilesConfig;

            Dispatcher.UIThread.Post(() =>
            {
                GsiEndpoint = BuildEndpointDisplay(systemConfig.Gsi);

                var profile = ResolveActiveProfile(profilesConfig);
                _activeProfileId = profile.Id;
                _activeProfileName = profile.Name;

                foreach (var eventSettings in EventSettings)
                {
                    if (profile.Rules.TryGetValue(eventSettings.EventType, out var rule))
                    {
                        eventSettings.SetRule(rule);
                    }
                    else
                    {
                        eventSettings.SetRule(new EventRule(EventAction.None, new List<string>(), null));
                    }
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
        if (_systemConfig is null || _profilesConfig is null)
        {
            return;
        }

        IsSaving = true;
        ErrorMessage = string.Empty;
        StatusMessage = "Saving configuration...";

        try
        {
            var rules = EventSettings.ToDictionary(
                item => item.EventType,
                item => item.GetRule());

            var profile = new MusicProfile(
                _activeProfileId,
                _activeProfileName,
                rules);

            var profiles = new MusicProfilesConfig(
                _activeProfileId,
                new List<MusicProfile> { profile });

            await _profileService.SaveAsync(profiles).ConfigureAwait(false);

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

    private static MusicProfile ResolveActiveProfile(MusicProfilesConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.ActiveProfileId))
        {
            var match = config.Profiles.FirstOrDefault(profile => profile.Id == config.ActiveProfileId);
            if (match is not null)
            {
                return match;
            }
        }

        return config.Profiles.FirstOrDefault()
               ?? new MusicProfile("default", "Default", new Dictionary<EventType, EventRule>());
    }

    private static string BuildEndpointDisplay(GsiConfig endpointInfo)
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
