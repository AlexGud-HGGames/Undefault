using Core.Models;

namespace Core.Actions.Spotify;

public sealed class SpotifyActionOptions
{
    public Dictionary<EventType, string> EventPlaylistMap { get; init; } = new();
    public Dictionary<EventType, int> EventVolumeMap { get; init; } = new();
    public string? DefaultPlaylistUri { get; init; }
    public int? DefaultVolume { get; init; }
}
