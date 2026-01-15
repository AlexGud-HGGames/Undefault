using System.Text;
using System.Text.Json;
using Core.Spotify.Models;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Spotify;

public sealed class SpotifyOAuthService
{
    private readonly SpotifyClientOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SpotifyOAuthService> _logger;

    public SpotifyOAuthService(
        IHttpClientFactory httpClientFactory,
        IOptions<SpotifyClientOptions> options,
        ILogger<SpotifyOAuthService> logger)
    {
        _options = options.Value;
        _httpClient = httpClientFactory.CreateClient("SpotifyOAuth");
        _logger = logger;
    }

    public string GetAuthorizationUrl(string state)
    {
        var scopes = string.Join(" ", _options.Scopes);
        var redirectUri = Uri.EscapeDataString(_options.RedirectUri);
        var stateEncoded = Uri.EscapeDataString(state);

        return $"https://accounts.spotify.com/authorize?" +
               $"client_id={_options.ClientId}" +
               $"&response_type=code" +
               $"&redirect_uri={redirectUri}" +
               $"&scope={Uri.EscapeDataString(scopes)}" +
               $"&state={stateEncoded}";
    }

    public async Task<SpotifyAuthResult> ExchangeCodeForTokenAsync(string authorizationCode, CancellationToken cancellationToken = default)
    {
        var requestBody = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = authorizationCode,
            ["redirect_uri"] = _options.RedirectUri
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
        {
            Content = new FormUrlEncodedContent(requestBody)
        };

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (tokenResponse is null || string.IsNullOrEmpty(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException("Failed to parse token response");
        }

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

        return new SpotifyAuthResult(
            AccessToken: tokenResponse.AccessToken,
            RefreshToken: tokenResponse.RefreshToken ?? string.Empty,
            ExpiresAt: expiresAt,
            Scopes: tokenResponse.Scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>()
        );
    }

    public async Task<SpotifyAuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var requestBody = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
        {
            Content = new FormUrlEncodedContent(requestBody)
        };

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (tokenResponse is null || string.IsNullOrEmpty(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException("Failed to parse refresh token response");
        }

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

        return new SpotifyAuthResult(
            AccessToken: tokenResponse.AccessToken,
            RefreshToken: tokenResponse.RefreshToken ?? refreshToken,
            ExpiresAt: expiresAt,
            Scopes: tokenResponse.Scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>()
        );
    }

    private sealed class TokenResponse
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public int ExpiresIn { get; set; }
        public string? Scope { get; set; }
    }
}
