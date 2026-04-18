namespace Core.Spotify;

public sealed class SmartTrackStartOptions
{
    public bool Enabled { get; init; } = false;

    public bool PreloadOnStartup { get; init; } = true;
}
