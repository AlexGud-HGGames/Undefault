using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Core.Spotify.Models;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Spotify;

public sealed class SpotifyClient : ISpotifyClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorage _tokenStorage;
    private readonly SpotifyOAuthService _oauthService;
    private readonly SpotifyClientOptions _options;
    private readonly ILogger<SpotifyClient> _logger;

    public SpotifyClient(
        IHttpClientFactory httpClientFactory,
        ITokenStorage tokenStorage,
        SpotifyOAuthService oauthService,
        IOptions<SpotifyClientOptions> options,
        ILogger<SpotifyClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("SpotifyApi");
        _tokenStorage = tokenStorage;
        _oauthService = oauthService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PlaybackState?> GetCurrentPlaybackAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/me/player");
        await AddAuthorizationHeaderAsync(request, cancellationToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var playbackResponse = JsonSerializer.Deserialize<PlaybackResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (playbackResponse is null)
        {
            return null;
        }

        return MapToPlaybackState(playbackResponse);
    }

    public async Task PlayAsync(string? uri = null, int? positionMs = null, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        var request = new HttpRequestMessage(HttpMethod.Put, "https://api.spotify.com/v1/me/player/play");
        await AddAuthorizationHeaderAsync(request, cancellationToken);

        if (!string.IsNullOrEmpty(uri) || positionMs.HasValue)
        {
            var body = JsonSerializer.Serialize(new
            {
                uris = string.IsNullOrWhiteSpace(uri) ? null : new[] { uri },
                position_ms = positionMs
            });
            request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        var request = new HttpRequestMessage(HttpMethod.Put, "https://api.spotify.com/v1/me/player/pause");
        await AddAuthorizationHeaderAsync(request, cancellationToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        var request = new HttpRequestMessage(HttpMethod.Put, "https://api.spotify.com/v1/me/player/play");
        await AddAuthorizationHeaderAsync(request, cancellationToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetVolumeAsync(int volume, CancellationToken cancellationToken = default)
    {
        if (volume < 0 || volume > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(volume), "Volume must be between 0 and 100");
        }

        await EnsureAuthenticatedAsync(cancellationToken);

        var request = new HttpRequestMessage(HttpMethod.Put, $"https://api.spotify.com/v1/me/player/volume?volume_percent={volume}");
        await AddAuthorizationHeaderAsync(request, cancellationToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        var accessToken = await _tokenStorage.GetAccessTokenAsync(cancellationToken);
        var expiresAt = await _tokenStorage.GetExpiresAtAsync(cancellationToken);

        if (string.IsNullOrEmpty(accessToken))
        {
            return false;
        }

        if (expiresAt.HasValue && expiresAt.Value <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        return true;
    }

    public Task<string> GetAuthorizationUrlAsync(string state, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_oauthService.GetAuthorizationUrl(state));
    }

    public async Task<SpotifyAuthResult> AuthenticateAsync(string authorizationCode, CancellationToken cancellationToken = default)
    {
        var result = await _oauthService.ExchangeCodeForTokenAsync(authorizationCode, cancellationToken);
        await _tokenStorage.SaveTokensAsync(result.AccessToken, result.RefreshToken, result.ExpiresAt, cancellationToken);
        return result;
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        var accessToken = await _tokenStorage.GetAccessTokenAsync(cancellationToken);
        var expiresAt = await _tokenStorage.GetExpiresAtAsync(cancellationToken);

        if (string.IsNullOrEmpty(accessToken))
        {
            throw new InvalidOperationException("Not authenticated. Call AuthenticateAsync first.");
        }

        if (expiresAt.HasValue && expiresAt.Value <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            var refreshToken = await _tokenStorage.GetRefreshTokenAsync(cancellationToken);
            if (string.IsNullOrEmpty(refreshToken))
            {
                throw new InvalidOperationException("Access token expired and no refresh token available.");
            }

            _logger.LogInformation("Refreshing access token");
            var newResult = await _oauthService.RefreshTokenAsync(refreshToken, cancellationToken);
            await _tokenStorage.SaveTokensAsync(newResult.AccessToken, newResult.RefreshToken, newResult.ExpiresAt, cancellationToken);
        }
    }

    private async Task AddAuthorizationHeaderAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = await _tokenStorage.GetAccessTokenAsync(cancellationToken);
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
    }

    private static PlaybackState MapToPlaybackState(PlaybackResponse response)
    {
        Track? track = null;
        if (response.Item is not null)
        {
            var artists = response.Item.Artists?.Select(a => new Artist(a.Id ?? string.Empty, a.Name ?? string.Empty)).ToList() ?? new List<Artist>();
            var album = response.Item.Album is not null
                ? new Album(
                    response.Item.Album.Id ?? string.Empty,
                    response.Item.Album.Name ?? string.Empty,
                    response.Item.Album.Images?.FirstOrDefault()?.Url)
                : null;

            track = new Track(
                response.Item.Id ?? string.Empty,
                response.Item.Name ?? string.Empty,
                response.Item.Uri,
                response.Item.DurationMs,
                artists,
                album
            );
        }

        return new PlaybackState(
            IsPlaying: response.IsPlaying ?? false,
            VolumePercent: response.Device?.VolumePercent,
            Track: track,
            DeviceId: response.Device?.Id,
            DeviceName: response.Device?.Name
        );
    }

    private sealed class PlaybackResponse
    {
        public bool? IsPlaying { get; set; }
        public DeviceResponse? Device { get; set; }
        public TrackItemResponse? Item { get; set; }
    }

    private sealed class DeviceResponse
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public int? VolumePercent { get; set; }
    }

    private sealed class TrackItemResponse
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Uri { get; set; }
        public int? DurationMs { get; set; }
        public List<ArtistResponse>? Artists { get; set; }
        public AlbumResponse? Album { get; set; }
    }

    private sealed class ArtistResponse
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class AlbumResponse
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public List<ImageResponse>? Images { get; set; }
    }

    private sealed class ImageResponse
    {
        public string? Url { get; set; }
    }
}
