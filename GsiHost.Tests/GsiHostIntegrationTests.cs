using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Spotify;
using Core.Spotify.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace GsiHost.Tests;

public sealed class GsiHostIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public GsiHostIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = CreateClientWithSpotify(new FakeSpotifyClient());
    }

    [Fact]
    public async Task GsiEndpoint_AcceptsPayload_AndCreatesEvents()
    {
        var response1 = await _client.PostAsJsonAsync("/gsi", CreatePayload(1000, 100));
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        var response2 = await _client.PostAsJsonAsync("/gsi", CreatePayload(1001, 0));
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response2.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var eventsCount = doc.RootElement.GetProperty("events").GetInt32();

        eventsCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task StatusAndEventsEndpoints_ReturnData()
    {
        var status = await _client.GetAsync("/status");
        status.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = await _client.GetAsync("/events");
        events.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GsiEndpoint_AllowsEmptyPayload()
    {
        var response = await _client.PostAsJsonAsync("/gsi", new { });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StatusEndpoint_HandlesSpotifyFailure()
    {
        var client = CreateClientWithSpotify(new ThrowingSpotifyClient());

        var response = await client.PostAsJsonAsync("/gsi", CreatePayload(1000, 100));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await client.GetAsync("/status");
        status.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static object CreatePayload(long timestamp, int health)
    {
        return new
        {
            provider = new { timestamp },
            map = new { matchid = "match" },
            player = new
            {
                steamid = "player",
                activity = "playing",
                state = new { health, armor = 0 }
            }
        };
    }

    private sealed class FakeSpotifyClient : ISpotifyClient
    {
        public Task<PlaybackState?> GetCurrentPlaybackAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PlaybackState?>(null);
        }

        public Task PlayAsync(string? uri = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetVolumeAsync(int volume, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<string> GetAuthorizationUrlAsync(string state, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public Task<SpotifyAuthResult> AuthenticateAsync(string authorizationCode, CancellationToken cancellationToken = default)
            => Task.FromResult(new SpotifyAuthResult(string.Empty, string.Empty, DateTimeOffset.UtcNow, Array.Empty<string>()));
    }

    private sealed class ThrowingSpotifyClient : ISpotifyClient
    {
        public Task<PlaybackState?> GetCurrentPlaybackAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Spotify unavailable");

        public Task PlayAsync(string? uri = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetVolumeAsync(int volume, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string> GetAuthorizationUrlAsync(string state, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public Task<SpotifyAuthResult> AuthenticateAsync(string authorizationCode, CancellationToken cancellationToken = default)
            => Task.FromResult(new SpotifyAuthResult(string.Empty, string.Empty, DateTimeOffset.UtcNow, Array.Empty<string>()));
    }

    private HttpClient CreateClientWithSpotify(ISpotifyClient spotifyClient)
    {
        var customizedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ISpotifyClient>(_ => spotifyClient);
            });
        });

        return customizedFactory.CreateClient();
    }
}
