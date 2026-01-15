namespace Core.Spotify;

public sealed class InMemoryTokenStorage : ITokenStorage
{
    private readonly object _lock = new();
    private string? _accessToken;
    private string? _refreshToken;
    private DateTimeOffset? _expiresAt;

    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_accessToken);
        }
    }

    public Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_refreshToken);
        }
    }

    public Task<DateTimeOffset?> GetExpiresAtAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_expiresAt);
        }
    }

    public Task SaveTokensAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _accessToken = accessToken;
            _refreshToken = refreshToken;
            _expiresAt = expiresAt;
        }

        return Task.CompletedTask;
    }

    public Task ClearTokensAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _accessToken = null;
            _refreshToken = null;
            _expiresAt = null;
        }

        return Task.CompletedTask;
    }
}
