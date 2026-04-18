namespace Core.Music;

/// <summary>
/// Debug / UI snapshot for observability. See docs/music-engine-config-schema-v1.md.
/// </summary>
public sealed class MusicEngineDebugSnapshot
{
    public DateTimeOffset CapturedAtUtc { get; init; }

    public MusicSafetyState DesiredSafetyState { get; init; }

    public string? LastSafetyTransitionReason { get; init; }

    public GameClockSnapshot? Clock { get; init; }

    public IReadOnlyDictionary<string, string>? MixerChannelContributions { get; init; }

    public int? LastMergedVolumePercent { get; init; }

    public IReadOnlyList<DeviceCommandLogEntry>? LastDeviceCommands { get; init; }

    public bool DeviceDegraded { get; init; }

    public string? LastSpotifyError { get; init; }
}

public readonly record struct DeviceCommandLogEntry(
    string Action,
    bool Success,
    DateTimeOffset TimestampUtc);
