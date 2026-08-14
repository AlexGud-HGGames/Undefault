namespace Core.Music;

/// <summary>
/// Generic playback transport status independent of a specific player backend.
/// </summary>
public enum PlaybackStatus
{
    /// <summary>
    /// Playback is actively producing audio.
    /// </summary>
    Playing,

    /// <summary>
    /// Playback is paused and can be resumed.
    /// </summary>
    Paused,

    /// <summary>
    /// Playback is stopped.
    /// </summary>
    Stopped,

    /// <summary>
    /// The player reported a status that is not mapped.
    /// </summary>
    Unknown
}
