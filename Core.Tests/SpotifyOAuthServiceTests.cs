using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Core.Spotify;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Core.Tests;

public sealed class SpotifyOAuthServiceTests
{
    [Fact]
    public void GetAuthorizationUrl_UsesExplicitLoopbackRedirectUri()
    {
        var service = CreateService(new SpotifyClientOptions
        {
            ClientId = "client-id",
            RedirectUri = "http://127.0.0.1:5292/callback",
            Scopes = new[] { "user-modify-playback-state", "user-read-playback-state" }
        });

        var url = service.GetAuthorizationUrl();

        url.Should().Contain("redirect_uri=http%3A%2F%2F127.0.0.1%3A5292%2Fcallback");
    }

    [Fact]
    public void GetAuthorizationUrl_RejectsLocalhostRedirectUri()
    {
        var service = CreateService(new SpotifyClientOptions
        {
            ClientId = "client-id",
            RedirectUri = "http://localhost:5292/callback"
        });

        var act = () => service.GetAuthorizationUrl();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot use localhost*");
    }

    [Fact]
    public void GetAuthorizationUrl_AddsPkceChallengeAndS256Method()
    {
        var service = CreateService(new SpotifyClientOptions
        {
            ClientId = "client-id",
            RedirectUri = "http://127.0.0.1:5292/callback"
        });

        var url = service.GetAuthorizationUrl();

        var query = HttpUtility.ParseQueryString(new Uri(url).Query);
        query["code_challenge_method"].Should().Be("S256");
        var challenge = query["code_challenge"];
        challenge.Should().NotBeNullOrEmpty();
        // SHA-256 base64url-no-pad is exactly 43 characters.
        challenge!.Length.Should().Be(43);
        challenge.Should().NotContain("=").And.NotContain("+").And.NotContain("/");
    }

    [Fact]
    public async Task ExchangeCodeForTokenAsync_PostsPkceBody_NoBasicAuthHeader_AndChallengeMatchesVerifier()
    {
        var handler = new RecordingHandler();
        var service = CreateService(
            new SpotifyClientOptions
            {
                ClientId = "client-id",
                RedirectUri = "http://127.0.0.1:5292/callback"
            },
            handler);

        var url = service.GetAuthorizationUrl(state: "csrf-state");
        var query = HttpUtility.ParseQueryString(new Uri(url).Query);
        var emittedChallenge = query["code_challenge"]!;

        await service.ExchangeCodeForTokenAsync("auth-code", state: "csrf-state");

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.Authorization.Should().BeNull(
            "PKCE flow drops the Authorization: Basic header");

        handler.LastFormBody.Should().NotBeNull();
        handler.LastFormBody!["grant_type"].Should().Be("authorization_code");
        handler.LastFormBody["code"].Should().Be("auth-code");
        handler.LastFormBody["redirect_uri"].Should().Be("http://127.0.0.1:5292/callback");
        handler.LastFormBody["client_id"].Should().Be("client-id");

        var verifier = handler.LastFormBody["code_verifier"];
        verifier.Should().NotBeNullOrEmpty();
        verifier!.Length.Should().BeInRange(43, 128, "RFC 7636 §4.1");

        var recomputed = Base64UrlNoPad(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        recomputed.Should().Be(emittedChallenge,
            "the body's code_verifier must hash to the previously-emitted code_challenge");
    }

    [Fact]
    public async Task ExchangeCodeForTokenAsync_WithoutPriorAuthorize_Throws()
    {
        var service = CreateService(
            new SpotifyClientOptions { ClientId = "client-id" },
            new RecordingHandler());

        await FluentActions.Awaiting(() => service.ExchangeCodeForTokenAsync("auth-code"))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*code_verifier missing*");
    }

    [Fact]
    public async Task RefreshTokenAsync_PostsBodyWithClientId_NoBasicAuthHeader()
    {
        var handler = new RecordingHandler();
        var service = CreateService(
            new SpotifyClientOptions
            {
                ClientId = "client-id",
                RedirectUri = "http://127.0.0.1:5292/callback"
            },
            handler);

        await service.RefreshTokenAsync("refresh-token-value");

        handler.LastRequest!.Headers.Authorization.Should().BeNull();
        handler.LastFormBody!["grant_type"].Should().Be("refresh_token");
        handler.LastFormBody["refresh_token"].Should().Be("refresh-token-value");
        handler.LastFormBody["client_id"].Should().Be("client-id");
    }

    private static SpotifyOAuthService CreateService(SpotifyClientOptions options, HttpMessageHandler? handler = null)
    {
        return new SpotifyOAuthService(
            new FakeHttpClientFactory(handler),
            Options.Create(options));
    }

    private static string Base64UrlNoPad(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler? _handler;

        public FakeHttpClientFactory(HttpMessageHandler? handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
        {
            return _handler is null ? new HttpClient() : new HttpClient(_handler, disposeHandler: false);
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public IReadOnlyDictionary<string, string>? LastFormBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;

            if (request.Content is not null)
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                var parsed = HttpUtility.ParseQueryString(body);
                var dict = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (string? key in parsed)
                {
                    if (key is null) continue;
                    dict[key] = parsed[key] ?? string.Empty;
                }

                LastFormBody = dict;
            }

            const string responseJson = """
            {
              "access_token": "test-access-token",
              "token_type": "Bearer",
              "scope": "user-modify-playback-state user-read-playback-state",
              "expires_in": 3600,
              "refresh_token": "test-refresh-token"
            }
            """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
