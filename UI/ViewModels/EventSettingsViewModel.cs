using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Core.Models;

namespace UI.ViewModels;

public sealed class EventSettingsViewModel : ViewModelBase
{
    public EventSettingsViewModel(EventType eventType)
    {
        EventType = eventType;
        ActionKeys = new ObservableCollection<EditableStringItem>();
        PlaylistUris = new ObservableCollection<EditableStringItem>();

        AddActionKeyCommand = new DelegateCommand(() => ActionKeys.Add(CreateItem(string.Empty, ActionKeys)));
        AddPlaylistUriCommand = new DelegateCommand(() => PlaylistUris.Add(CreateItem(string.Empty, PlaylistUris)));
    }

    public EventType EventType { get; }

    public string Title => EventType.ToString();

    public ObservableCollection<EditableStringItem> ActionKeys { get; }

    public ObservableCollection<EditableStringItem> PlaylistUris { get; }

    public ICommand AddActionKeyCommand { get; }

    public ICommand AddPlaylistUriCommand { get; }

    public void SetActionKeys(IEnumerable<string> values)
    {
        ActionKeys.Clear();
        foreach (var value in values)
        {
            ActionKeys.Add(CreateItem(value, ActionKeys));
        }
    }

    public void SetPlaylistUris(IEnumerable<string> values)
    {
        PlaylistUris.Clear();
        foreach (var value in values)
        {
            PlaylistUris.Add(CreateItem(value, PlaylistUris));
        }
    }

    public List<string> GetActionKeys()
    {
        return ActionKeys
            .Select(item => item.Value.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }

    public List<string> GetPlaylistUris()
    {
        return PlaylistUris
            .Select(item => item.Value.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }

    private static EditableStringItem CreateItem(string value, ObservableCollection<EditableStringItem> collection)
    {
        return new EditableStringItem(value, item => collection.Remove(item));
    }
}
