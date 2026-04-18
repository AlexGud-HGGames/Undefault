namespace Core.Music;

/// <summary>
/// Versioned music engine options for host persistence and UI. See docs/music-engine-config-schema-v1.md.
/// </summary>
public sealed class MusicEngineOptionsV1
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Max age of last observation before Unknown/Danger escalation.</summary>
    public int StaleObservationMs { get; set; } = 2_000;

    public bool StaleEscalatesToDanger { get; set; }

    public int DangerExitHysteresisMs { get; set; } = 500;

    public int? FloorVolumePercent { get; set; }

    public bool ForbidFloorInDanger { get; set; } = true;

    public int EmergencyEngineSlaMs { get; set; } = 50;

    public int VolumeEpsilonPercent { get; set; } = 1;

    public int MinVolumeCommandIntervalMs { get; set; } = 150;
}
