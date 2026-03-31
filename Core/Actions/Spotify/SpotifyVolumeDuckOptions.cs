namespace Core.Actions.Spotify;

public sealed class SpotifyVolumeDuckOptions
{
    public int MuteVolume { get; init; } = 0;
    public int FallbackRestoreVolume { get; init; } = 50;
}
