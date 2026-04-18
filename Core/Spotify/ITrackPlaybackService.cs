namespace Core.Spotify;

public interface ITrackPlaybackService
{
    Task PlayTrackAsync(string trackUri, CancellationToken cancellationToken = default);
}
