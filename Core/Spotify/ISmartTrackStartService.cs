namespace Core.Spotify;

public interface ISmartTrackStartService
{
    string FilePath { get; }

    Task WarmAsync(CancellationToken cancellationToken = default);

    Task<int?> ResolveStartPositionMsAsync(string trackUri, CancellationToken cancellationToken = default);
}
