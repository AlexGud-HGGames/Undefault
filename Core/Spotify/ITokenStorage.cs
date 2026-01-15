namespace Core.Spotify;

public interface ITokenStorage
{
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default);
    Task<DateTimeOffset?> GetExpiresAtAsync(CancellationToken cancellationToken = default);
    Task SaveTokensAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
    Task ClearTokensAsync(CancellationToken cancellationToken = default);
}
