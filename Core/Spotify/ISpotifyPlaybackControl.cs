using Core.Configuration;
using Core.Models;

namespace Core.Spotify;

/// <summary>
/// Shared Spotify pause / resume / duck / restore semantics for control profiles and scenario orchestration.
/// </summary>
public interface ISpotifyPlaybackControl
{
    Task TryPauseAsync(string? eventKeyForLog, CancellationToken cancellationToken = default);

    Task TryResumeAsync(string? eventKeyForLog, CancellationToken cancellationToken = default);

    Task TryDuckAsync(
        EventControlRule rule,
        NormalizedEvent context,
        CancellationToken cancellationToken = default);

    Task TryDuckAsync(
        int volumePercent,
        string? eventKeyForLog,
        CancellationToken cancellationToken = default);

    Task TryRestoreVolumeAsync(string? eventKeyForLog, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets playback volume while a managed session is active; if inactive, captures current volume as restore target then applies.
    /// </summary>
    Task TrySetManagedVolumeAsync(
        int volumePercent,
        string? eventKeyForLog,
        CancellationToken cancellationToken = default);
}
