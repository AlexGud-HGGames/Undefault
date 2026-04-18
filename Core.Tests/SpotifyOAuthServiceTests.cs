using System.Net.Http;
using Core.Spotify;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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
            ClientSecret = "client-secret",
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
            ClientSecret = "client-secret",
            RedirectUri = "http://localhost:5292/callback"
        });

        var act = () => service.GetAuthorizationUrl();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot use localhost*");
    }

    private static SpotifyOAuthService CreateService(SpotifyClientOptions options)
    {
        return new SpotifyOAuthService(
            new FakeHttpClientFactory(),
            Options.Create(options),
            NullLogger<SpotifyOAuthService>.Instance);
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }
}
