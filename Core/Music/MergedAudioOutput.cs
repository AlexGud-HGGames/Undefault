namespace Core.Music;

public enum TransportCommandKind
{
    NoChange = 0,
    Pause = 1,
    Resume = 2,
}

/// <summary>
/// Result of mixer evaluation before device coalescing.
/// </summary>
public readonly record struct MergedAudioOutput(
    int? TargetVolumePercent,
    TransportCommandKind Transport,
    bool HardSuppressAudio);
