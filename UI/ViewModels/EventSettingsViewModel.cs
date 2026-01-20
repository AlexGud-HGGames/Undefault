using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Core.Configuration;
using Core.Models;

namespace UI.ViewModels;

public sealed class EventSettingsViewModel : ViewModelBase
{
    public EventSettingsViewModel(EventType eventType)
    {
        EventType = eventType;
        PlaylistUris = new ObservableCollection<EditableStringItem>();

        AddPlaylistUriCommand = new DelegateCommand(() => PlaylistUris.Add(CreateItem(string.Empty, PlaylistUris)));
    }

    public EventType EventType { get; }

    public string Title => EventType.ToString();

    public ObservableCollection<EditableStringItem> PlaylistUris { get; }

    public ICommand AddPlaylistUriCommand { get; }

    public Array AvailableActions => Enum.GetValues<EventAction>();

    public EventAction SelectedAction
    {
        get => _selectedAction;
        set => SetField(ref _selectedAction, value);
    }

    public string VolumeText
    {
        get => _volumeText;
        set => SetField(ref _volumeText, value);
    }

    public void SetPlaylistUris(IEnumerable<string> values)
    {
        PlaylistUris.Clear();
        foreach (var value in values)
        {
            PlaylistUris.Add(CreateItem(value, PlaylistUris));
        }
    }

    public List<string> GetPlaylistUris()
    {
        return PlaylistUris
            .Select(item => item.Value.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }

    public void SetRule(EventRule rule)
    {
        SelectedAction = rule.Action;
        VolumeText = rule.Volume?.ToString() ?? string.Empty;
        SetPlaylistUris(rule.Tracks);
    }

    public EventRule GetRule()
    {
        var volume = int.TryParse(VolumeText, out var parsed)
            ? parsed
            : (int?)null;

        return new EventRule(SelectedAction, GetPlaylistUris(), volume);
    }

    private static EditableStringItem CreateItem(string value, ObservableCollection<EditableStringItem> collection)
    {
        return new EditableStringItem(value, item => collection.Remove(item));
    }

    private EventAction _selectedAction;
    private string _volumeText = string.Empty;
}
