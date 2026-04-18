namespace Core.Music;

/// <summary>
/// Inputs for <see cref="IMusicMixer"/> from safety controller and policies.
/// </summary>
public readonly record struct MusicMixerContext(
    MusicSafetyState SafetyState,
    int BaseVolumePercent,
    int? FloorVolumePercent,
    int CeilingVolumePercent,
    bool ForbidFloorInDanger);
