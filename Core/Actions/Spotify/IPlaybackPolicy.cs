using Core.Models;

namespace Core.Actions.Spotify;

public interface IPlaybackPolicy
{
    Task BeforePlayAsync(NormalizedEvent normalizedEvent, CancellationToken cancellationToken = default);
}
