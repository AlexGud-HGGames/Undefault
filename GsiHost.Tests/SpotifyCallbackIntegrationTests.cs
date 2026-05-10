using System.Net;
using System.Text;
using System.Web;
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
    public async Task Callback_StoresTokenInMemory_AndReturnsHtml_AndUsesPkceWireFormat()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "UndefaultIt.Tests", Guid.NewGuid().ToString("N"));
        var cs2Root = Path.Combine(tempRoot, "Counter-Strike Global Offensive");
        Directory.CreateDirectory(Path.Combine(cs2Root, "game", "csgo", "cfg"));
        Directory.CreateDirectory(tempRoot);
        await File.WriteAllTextAsync(Path.Combine(tempRoot, "appsettings.json"), BuildAppSettingsJson());

        var previousOverride = Environment.GetEnvironmentVariable("UNDEFAULTIT_CS2_PATH");
        Environment.SetEnvironmentVariable("UNDEFAULTIT_CS2_PATH", cs2Root);

        var fakeFactory = new FakeHttpClientFactory();

        try
        {
            using var customizedFactory = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting(WebHostDefaults.ContentRootKey, tempRoot);
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<SpotifyOAuthService>(_ =>
                        new SpotifyOAuthService(
                            fakeFactory,
                            Options.Create(new SpotifyClientOptions
                            {
                                ClientId = "spotify-client-id",
                                RedirectUri = "http://127.0.0.1:5292/callback",
                                Scopes = new[] { "user-modify-playback-state", "user-read-playback-state" }
                            }),
                            NullLogger<SpotifyOAuthService>.Instance));
                });
            });

            using var client = customizedFactory.CreateClient();

            // Pre-populate a PKCE verifier for the callback's state value.
            var oauthService = customizedFactory.Services.GetRequiredService<SpotifyOAuthService>();
            const string state = "round-trip-state";
            _ = oauthService.GetAuthorizationUrl(state);

            var response = await client.GetAsync($"/callback?code=test-authorization-code&state={state}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
            var html = await response.Content.ReadAsStringAsync();
            html.Should().Contain("Spotify connected, you can close this window.");

            var tokenStorage = customizedFactory.Services.GetRequiredService<ITokenStorage>();
            (await tokenStorage.GetAccessTokenAsync()).Should().Be("test-access-token");

            // PKCE assertions on the captured wire request.
            fakeFactory.Handler.LastRequest.Should().NotBeNull();
            fakeFactory.Handler.LastRequest!.Headers.Authorization.Should().BeNull(
                "PKCE flow drops the Authorization: Basic header");
            fakeFactory.Handler.LastFormBody!["client_id"].Should().Be("spotify-client-id");
            fakeFactory.Handler.LastFormBody.Should().ContainKey("code_verifier");
            fakeFactory.Handler.LastFormBody["code_verifier"].Should().NotBeNullOrEmpty();
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
        public FakeTokenHandler Handler { get; } = new();

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(Handler, disposeHandler: false);
        }
    }

    private sealed class FakeTokenHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public IReadOnlyDictionary<string, string>? LastFormBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            request.RequestUri!.ToString().Should().Be("https://accounts.spotify.com/api/token");

            if (request.Content is not null)
            {
                var requestBody = await request.Content.ReadAsStringAsync(cancellationToken);
                var parsed = HttpUtility.ParseQueryString(requestBody);
                var dict = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (string? key in parsed)
                {
                    if (key is null) continue;
                    dict[key] = parsed[key] ?? string.Empty;
                }

                LastFormBody = dict;
            }

            const string responseBody = """
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
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
