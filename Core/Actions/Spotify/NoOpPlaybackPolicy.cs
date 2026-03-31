using Core.Models;

namespace Core.Actions.Spotify;

public sealed class NoOpPlaybackPolicy : IPlaybackPolicy
{
    public Task BeforePlayAsync(NormalizedEvent normalizedEvent, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
