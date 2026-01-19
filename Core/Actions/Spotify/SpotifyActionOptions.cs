using Core.Models;

namespace Core.Actions.Spotify;

public sealed class SpotifyActionOptions
{
    public Dictionary<EventType, List<string>> EventPlaylistMap { get; init; } = new();
    public Dictionary<EventType, int> EventVolumeMap { get; init; } = new();
    public List<string> DefaultPlaylistUris { get; init; } = new();
    public int? DefaultVolume { get; init; }
}
