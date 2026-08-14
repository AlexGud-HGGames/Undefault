namespace Core.Music;

/// <summary>
/// Declares which MVP transport operations a player adapter supports.
/// </summary>
/// <param name="CanPlay">Gets a value that indicates whether <c>Play</c> is supported.</param>
/// <param name="CanPause">Gets a value that indicates whether <c>Pause</c> is supported.</param>
/// <param name="CanResume">Gets a value that indicates whether <c>Resume</c> is supported.</param>
/// <param name="CanSkip">Gets a value that indicates whether next/previous are supported.</param>
/// <param name="CanSetVolume">Gets a value that indicates whether volume can be set.</param>
public sealed record MusicPlayerCapabilities(
    bool CanPlay,
    bool CanPause,
    bool CanResume,
    bool CanSkip,
    bool CanSetVolume)
{
    /// <summary>
    /// Gets capabilities for the Tauon and mock MVP adapters (play, pause, resume, skip, volume).
    /// </summary>
    public static MusicPlayerCapabilities Mvp { get; } = new(
        CanPlay: true,
        CanPause: true,
        CanResume: true,
        CanSkip: true,
        CanSetVolume: true);
}
