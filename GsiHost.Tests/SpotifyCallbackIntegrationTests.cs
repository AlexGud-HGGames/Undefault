using System.Net;
using System.Text;
using Core.Spotify;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GsiHost.Tests;

[Collection(Cs2SetupTestCollection.Name)]
public sealed class SpotifyCallbackIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SpotifyCallbackIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Callback_StoresTokenInMemory_AndReturnsHtml()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "UndefaultIt.Tests", Guid.NewGuid().ToString("N"));
        var cs2Root = Path.Combine(tempRoot, "Counter-Strike Global Offensive");
        Directory.CreateDirectory(Path.Combine(cs2Root, "game", "csgo", "cfg"));
        Directory.CreateDirectory(tempRoot);
        await File.WriteAllTextAsync(Path.Combine(tempRoot, "appsettings.json"), BuildAppSettingsJson());

        var previousOverride = Environment.GetEnvironmentVariable("UNDEFAULTIT_CS2_PATH");
        Environment.SetEnvironmentVariable("UNDEFAULTIT_CS2_PATH", cs2Root);

        try
        {
            using var customizedFactory = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting(WebHostDefaults.ContentRootKey, tempRoot);
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<SpotifyOAuthService>(_ =>
                        new SpotifyOAuthService(
                            new FakeHttpClientFactory(),
                            Options.Create(new SpotifyClientOptions
                            {
                                ClientId = "spotify-client-id",
                                ClientSecret = "spotify-client-secret",
                                RedirectUri = "http://127.0.0.1:5292/callback",
                                Scopes = new[] { "user-modify-playback-state", "user-read-playback-state" }
                            }),
                            NullLogger<SpotifyOAuthService>.Instance));
                });
            });

            using var client = customizedFactory.CreateClient();
            var response = await client.GetAsync("/callback?code=test-authorization-code");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
            var html = await response.Content.ReadAsStringAsync();
            html.Should().Contain("Spotify connected, you can close this window.");

            var tokenStorage = customizedFactory.Services.GetRequiredService<ITokenStorage>();
            (await tokenStorage.GetAccessTokenAsync()).Should().Be("test-access-token");
        }
        finally
        {
            Environment.SetEnvironmentVariable("UNDEFAULTIT_CS2_PATH", previousOverride);

            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string BuildAppSettingsJson()
    {
        return """
        {
          "Logging": {
            "LogLevel": {
              "Default": "Information",
              "Microsoft.AspNetCore": "Warning"
            }
          },
          "AllowedHosts": "*",
          "Spotify": {
            "ClientId": "spotify-client-id",
            "ClientSecret": "spotify-client-secret",
            "RedirectUri": "http://127.0.0.1:5292/callback",
            "Scopes": [
              "user-modify-playback-state",
              "user-read-playback-state"
            ]
          },
          "Gsi": {
            "Method": "POST",
            "Path": "/gsi",
            "Url": "http://127.0.0.1:5292"
          },
          "UseMockSpotify": false
        }
        """;
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new FakeTokenHandler());
        }
    }

    private sealed class FakeTokenHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.RequestUri!.ToString().Should().Be("https://accounts.spotify.com/api/token");
            request.Headers.Authorization.Should().NotBeNull();

            const string body = """
            {
              "access_token": "test-access-token",
              "token_type": "Bearer",
              "scope": "user-modify-playback-state user-read-playback-state",
              "expires_in": 3600,
              "refresh_token": "test-refresh-token"
            }
            """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
