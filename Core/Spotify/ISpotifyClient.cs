using Core.Spotify.Models;

namespace Core.Spotify;

public interface ISpotifyClient
{
    // Playback control
    Task<PlaybackState?> GetCurrentPlaybackAsync(CancellationToken cancellationToken = default);
    Task PlayAsync(string? uri = null, int? positionMs = null, CancellationToken cancellationToken = default);
    Task PauseAsync(CancellationToken cancellationToken = default);
    Task ResumeAsync(CancellationToken cancellationToken = default);
    Task SetVolumeAsync(int volume, CancellationToken cancellationToken = default);

    // Authentication
    Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default);
    Task<string> GetAuthorizationUrlAsync(string state, CancellationToken cancellationToken = default);
    Task<SpotifyAuthResult> AuthenticateAsync(string authorizationCode, CancellationToken cancellationToken = default);
}
