using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Spotify.Models;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Spotify;

/// <summary>
/// Authorization Code with PKCE flow for Spotify (RFC 7636 + Spotify PKCE tutorial).
/// No <c>client_secret</c> is sent on the wire and none is read from configuration —
/// the Spotify desktop client cannot keep one secret.
/// </summary>
public sealed class SpotifyOAuthService
{
    private const string ClientIdEnvVar = "CLIENT_ID";
    private const string DefaultRedirectUri = "http://127.0.0.1:5292/callback";

    // RFC 7636 §4.1: code_verifier MUST be 43..128 chars from [A-Z][a-z][0-9]-._~ .
    // We sample 64 cryptographically-random bytes and base64url-no-pad them, which gives
    // an 86-char verifier — well inside the legal range and well above the 43 minimum.
    private const int CodeVerifierEntropyBytes = 64;

    private readonly SpotifyClientOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SpotifyOAuthService> _logger;

    // Per-attempt verifier storage. Keyed by `state` when the caller supplies one, and
    // by a fixed sentinel for stateless callers (legacy console flow). Verifiers are
    // never persisted to disk, never logged, and are removed on consumption.
    private readonly ConcurrentDictionary<string, string> _verifiersByState = new(StringComparer.Ordinal);
    private const string StatelessSlot = "__stateless__";

    public SpotifyOAuthService(
        IHttpClientFactory httpClientFactory,
        IOptions<SpotifyClientOptions> options,
        ILogger<SpotifyOAuthService> logger)
    {
        _options = options.Value;
        _httpClient = httpClientFactory.CreateClient("SpotifyOAuth");
        _logger = logger;
    }

    /// <summary>
    /// True when a <c>client_id</c> is configured. PKCE removes the secret half of the
    /// pre-UND-47 "client credentials" check; only the public id is required now.
    /// </summary>
    public bool HasClientCredentials =>
        !string.IsNullOrWhiteSpace(GetClientId());

    public string RedirectUri => GetRedirectUri();

    public string GetAuthorizationUrl(string? state = null)
    {
        var clientId = GetClientId();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException("Spotify CLIENT_ID is not configured.");
        }

        var verifier = GenerateCodeVerifier();
        var challenge = ComputeCodeChallenge(verifier);

        var slot = string.IsNullOrWhiteSpace(state) ? StatelessSlot : state!;
        _verifiersByState[slot] = verifier;

        var scopes = string.Join(" ", _options.Scopes);
        var redirectUri = Uri.EscapeDataString(GetRedirectUri());

        var url = $"https://accounts.spotify.com/authorize?" +
               $"client_id={clientId}" +
               $"&response_type=code" +
               $"&redirect_uri={redirectUri}" +
               $"&scope={Uri.EscapeDataString(scopes)}" +
               $"&code_challenge_method=S256" +
               $"&code_challenge={challenge}";

        if (!string.IsNullOrWhiteSpace(state))
        {
            url += $"&state={Uri.EscapeDataString(state)}";
        }

        return url;
    }

    public Task<SpotifyAuthResult> ExchangeCodeForTokenAsync(string authorizationCode, CancellationToken cancellationToken = default)
    {
        return ExchangeCodeForTokenAsync(authorizationCode, state: null, cancellationToken);
    }

    public async Task<SpotifyAuthResult> ExchangeCodeForTokenAsync(string authorizationCode, string? state, CancellationToken cancellationToken = default)
    {
        var clientId = GetClientId();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException("Spotify CLIENT_ID is not configured.");
        }

        var slot = string.IsNullOrWhiteSpace(state) ? StatelessSlot : state!;
        if (!_verifiersByState.TryRemove(slot, out var verifier) || string.IsNullOrEmpty(verifier))
        {
            throw new InvalidOperationException(
                "PKCE code_verifier missing for this auth attempt. Call GetAuthorizationUrl first.");
        }

        var requestBody = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = authorizationCode,
            ["redirect_uri"] = GetRedirectUri(),
            ["client_id"] = clientId,
            ["code_verifier"] = verifier
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
        {
            Content = new FormUrlEncodedContent(requestBody)
        };

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
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException("Spotify CLIENT_ID is not configured.");
        }

        var requestBody = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
        {
            Content = new FormUrlEncodedContent(requestBody)
        };

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

    private static string GenerateCodeVerifier()
    {
        var bytes = new byte[CodeVerifierEntropyBytes];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlNoPad(bytes);
    }

    private static string ComputeCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlNoPad(hash);
    }

    private static string Base64UrlNoPad(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
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
