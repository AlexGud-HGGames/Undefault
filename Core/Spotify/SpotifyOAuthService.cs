using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;
using Core.Spotify.Models;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Spotify;

public sealed class SpotifyOAuthService
{
    private const string ClientIdEnvVar = "CLIENT_ID";
    private const string ClientSecretEnvVar = "CLIENT_SECRET";
    private const string DefaultRedirectUri = "http://127.0.0.1:5292/callback";

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

    public bool HasClientCredentials =>
        !string.IsNullOrWhiteSpace(GetClientId()) &&
        !string.IsNullOrWhiteSpace(GetClientSecret());

    public string RedirectUri => GetRedirectUri();

    public string GetAuthorizationUrl(string? state = null)
    {
        var clientId = GetClientId();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException("Spotify CLIENT_ID is not configured.");
        }

        var scopes = string.Join(" ", _options.Scopes);
        var redirectUri = Uri.EscapeDataString(GetRedirectUri());

        var url = $"https://accounts.spotify.com/authorize?" +
               $"client_id={clientId}" +
               $"&response_type=code" +
               $"&redirect_uri={redirectUri}" +
               $"&scope={Uri.EscapeDataString(scopes)}";

        if (!string.IsNullOrWhiteSpace(state))
        {
            url += $"&state={Uri.EscapeDataString(state)}";
        }

        return url;
    }

    public async Task<SpotifyAuthResult> ExchangeCodeForTokenAsync(string authorizationCode, CancellationToken cancellationToken = default)
    {
        var clientId = GetClientId();
        var clientSecret = GetClientSecret();
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("Spotify CLIENT_ID or CLIENT_SECRET is not configured.");
        }

        var requestBody = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = authorizationCode,
            ["redirect_uri"] = GetRedirectUri()
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
        {
            Content = new FormUrlEncodedContent(requestBody)
        };

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
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
        var clientId = GetClientId();
        var clientSecret = GetClientSecret();
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("Spotify CLIENT_ID or CLIENT_SECRET is not configured.");
        }

        var requestBody = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
        {
            Content = new FormUrlEncodedContent(requestBody)
        };

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
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
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }
    }

    private string GetClientId()
    {
        return Environment.GetEnvironmentVariable(ClientIdEnvVar) ?? _options.ClientId;
    }

    private string GetClientSecret()
    {
        return Environment.GetEnvironmentVariable(ClientSecretEnvVar) ?? _options.ClientSecret;
    }

    private string GetRedirectUri()
    {
        var configuredRedirectUri = string.IsNullOrWhiteSpace(_options.RedirectUri)
            ? DefaultRedirectUri
            : _options.RedirectUri.Trim();

        if (!Uri.TryCreate(configuredRedirectUri, UriKind.Absolute, out var redirectUri))
        {
            throw new InvalidOperationException("Spotify redirect URI must be an absolute URI.");
        }

        if (string.Equals(redirectUri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Spotify redirect URI cannot use localhost. Use an explicit loopback IP like http://127.0.0.1:5292/callback.");
        }

        if (redirectUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return redirectUri.ToString();
        }

        if (!redirectUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Spotify redirect URI must use HTTPS or HTTP on a loopback IP literal.");
        }

        if (!IPAddress.TryParse(redirectUri.Host, out var ipAddress) || !IPAddress.IsLoopback(ipAddress))
        {
            throw new InvalidOperationException(
                "Spotify redirect URI may use HTTP only with an explicit loopback IP literal such as 127.0.0.1 or [::1].");
        }

        return redirectUri.ToString();
    }
}
