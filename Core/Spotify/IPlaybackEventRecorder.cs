namespace Core.Spotify;

/// <summary>
/// Records confirmed Spotify playback state transitions (pause / resume) as an observe-only side effect.
/// </summary>
/// <remarks>
/// <para>
/// Implementations capture that a pause or resume actually changed playback state, so the event can be
/// persisted to the timeline. They must remain strictly observe-only: no Spotify API calls, no routing
/// through <c>RulesEngine.ActionMap</c>, and no dependency on host types.
/// </para>
/// <para>
/// Recording is invoked by <see cref="SpotifyPlaybackControlCoordinator"/> only after a successful,
/// state-changing pause or resume. No-op transitions (already paused / already playing), auth or device
/// failures, and exceptions are not recorded.
/// </para>
/// </remarks>
public interface IPlaybackEventRecorder
{
    /// <summary>
    /// Records that playback transitioned to paused at the supplied timestamp.
    /// </summary>
    /// <param name="timestampUtc">The UTC timestamp of the confirmed state transition.</param>
    /// <param name="cancellationToken">The token to cancel the record operation.</param>
    /// <returns>A task that represents the asynchronous record operation.</returns>
    Task RecordPausedAsync(DateTimeOffset timestampUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that playback transitioned to resumed (playing) at the supplied timestamp.
    /// </summary>
    /// <param name="timestampUtc">The UTC timestamp of the confirmed state transition.</param>
    /// <param name="cancellationToken">The token to cancel the record operation.</param>
    /// <returns>A task that represents the asynchronous record operation.</returns>
    Task RecordResumedAsync(DateTimeOffset timestampUtc, CancellationToken cancellationToken = default);
}
