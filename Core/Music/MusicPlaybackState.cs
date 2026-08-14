namespace Core.Music;

/// <summary>
/// Snapshot of player transport, optional current track, and volume.
/// </summary>
/// <param name="Status">Gets the mapped playback status.</param>
/// <param name="Track">Gets the current track when the backend reports one; otherwise <see langword="null"/>.</param>
/// <param name="VolumePercent">Gets the volume in the range 0–100 when known; otherwise <see langword="null"/>.</param>
public sealed record MusicPlaybackState(
    PlaybackStatus Status,
    MusicTrack? Track,
    int? VolumePercent);
