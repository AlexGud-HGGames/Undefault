using Core.Music;

namespace Core.Spotify;

/// <summary>
/// Compatibility alias for leftover Spotify DI. New code should use <see cref="IMusicPlaybackControl"/>.
/// </summary>
public interface ISpotifyPlaybackControl : IMusicPlaybackControl
{
}
