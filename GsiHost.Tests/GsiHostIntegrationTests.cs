using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Configuration;
using Core.Services;
using Core.Spotify;
using Core.Spotify.Models;
using Cs2Simulator.Runtime;
using Cs2Simulator.Scenarios.Scenarios;
using FluentAssertions;
using GsiHost.Adapters;
using GsiHost.Dtos;
using GsiHost.Mapping;
using GsiHost.Mapping.Modules;
using GsiHost.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GsiHost.Tests;

[Collection(Cs2SetupTestCollection.Name)]
public sealed class GsiHostIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GsiHostIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GsiEndpoint_AcceptsPayload_AndCreatesEvents()
    {
        using var host = CreateTestHost(new FakeSpotifyClient());

        var response1 = await host.Client.PostAsJsonAsync("/gsi", CreatePayload(1000, 100));
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        var response2 = await host.Client.PostAsJsonAsync("/gsi", CreatePayload(1001, 0));
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response2.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var eventsCount = doc.RootElement.GetProperty("events").GetInt32();

        eventsCount.Should().BeGreaterThanOrEqualTo(1);

        var appState = host.Factory.Services.GetRequiredService<AppStateService>();
        appState.GetRecentEvents().Count.Should().BeGreaterThanOrEqualTo(1);

        var eventsJson = await host.Client.GetStringAsync("/events");
        using (var evDoc = JsonDocument.Parse(eventsJson))
        {
            evDoc.RootElement.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
        }
    }

    [Fact]
    public async Task StatusAndEventsEndpoints_ReturnData()
    {
        using var host = CreateTestHost(new FakeSpotifyClient());

        var status = await host.Client.GetAsync("/status");
        status.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = await host.Client.GetAsync("/events");
        events.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GsiEndpoint_AllowsEmptyPayload()
    {
        using var host = CreateTestHost(new FakeSpotifyClient());

        var response = await host.Client.PostAsJsonAsync("/gsi", new { });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StatusEndpoint_HandlesSpotifyFailure()
    {
        using var host = CreateTestHost(new ThrowingSpotifyClient());

        var response = await host.Client.PostAsJsonAsync("/gsi", CreatePayload(1000, 100));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await host.Client.GetAsync("/status");
        status.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProfilesEndpoint_RoundTripsNewSchema()
    {
        using var host = CreateTestHost(new FakeSpotifyClient());

        var payload = new MusicProfilesConfig(
            "default",
            new List<MusicProfile>
            {
                new(
                    "default",
                    "Default",
                    new List<EventTrackRule>
                    {
                        new("death", new List<string> { "spotify:track:death_song" }),
                        new("custom:clutch_1v3", new List<string> { "spotify:track:clutch_song" })
                    })
            });

        var saveResponse = await host.Client.PutAsJsonAsync("/profiles", payload);
        saveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var roundTrip = await host.Client.GetFromJsonAsync<MusicProfilesConfig>("/profiles");

        roundTrip.Should().NotBeNull();
        roundTrip!.Profiles.Should().ContainSingle();
        roundTrip.Profiles[0].Rules.Should().HaveCount(2);
        roundTrip.Profiles[0].FindRule("CUSTOM:CLUTCH_1V3")!.Tracks.Should().ContainSingle("spotify:track:clutch_song");
    }

    [Fact]
    public async Task ControlProfilesEndpoint_RoundTripsConsoleControlSchema()
    {
        using var host = CreateTestHost(new FakeSpotifyClient());

        var payload = new ConsoleControlProfilesConfig(
            "console-default",
            new List<ConsoleControlProfile>
            {
                new(
                    "console-default",
                    "Console Default",
                    new List<EventControlRule>
                    {
                        new("round_start", MusicControlCommands.Duck, 15),
                        new("death", MusicControlCommands.RestoreVolume),
                        new("custom:music_off", MusicControlCommands.Pause),
                        new("custom:music_on", MusicControlCommands.Resume)
                    })
            });

        var saveResponse = await host.Client.PutAsJsonAsync("/control-profiles", payload);
        saveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var roundTrip = await host.Client.GetFromJsonAsync<ConsoleControlProfilesConfig>("/control-profiles");

        roundTrip.Should().NotBeNull();
        roundTrip!.Profiles.Should().ContainSingle();
        roundTrip.Profiles[0].Rules.Should().HaveCount(4);
        roundTrip.Profiles[0].FindRule("CUSTOM:MUSIC_OFF")!.Command.Should().Be(MusicControlCommands.Pause);
    }

    [Fact]
    public async Task SpotifyProfilePlayback_UsesSmartTrackStartOffset_WhenEnabled()
    {
        var spotifyClient = new FakeSpotifyClient
        {
            Authenticated = true
        };
        using var host = CreateTestHost(
            spotifyClient,
            appSettingsJson: BuildAppSettingsJson(
                "http://127.0.0.1:5292",
                enableSmartTrackStart: true,
                roundStartAction: "spotify.profile",
                deathAction: "spotify.control_profile"),
            seedContentRoot: tempRoot =>
            {
                File.WriteAllText(
                    Path.Combine(tempRoot, "profiles.json"),
                    """
                    {
                      "ActiveProfileId": "default",
                      "Profiles": [
                        {
                          "Id": "default",
                          "Name": "Default",
                          "Rules": [
                            {
                              "EventKey": "round_start",
                              "Tracks": [ "spotify:track:anthem" ]
                            }
                          ]
                        }
                      ]
                    }
                    """);

                File.WriteAllText(
                    Path.Combine(tempRoot, "smart-track-starts.json"),
                    """
                    {
                      "Entries": [
                        {
                          "TrackUri": "spotify:track:anthem",
                          "StartPositionMs": 42000,
                          "CueLabel": "drop"
                        }
                      ]
                    }
                    """);
            });

        await host.Client.PostAsJsonAsync("/gsi", CreatePayload(1000, 100, round: 4, phase: "freezetime"));
        var response = await host.Client.PostAsJsonAsync("/gsi", CreatePayload(1001, 100, round: 4, phase: "live"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        spotifyClient.PlayedUris.Should().ContainSingle().Which.Should().Be("spotify:track:anthem");
        spotifyClient.PlayedPositions.Should().ContainSingle().Which.Should().Be(42_000);
    }

    [Fact]
    public async Task SpotifyProfilePlayback_IgnoresSmartTrackStartMetadata_WhenDisabled()
    {
        var spotifyClient = new FakeSpotifyClient
        {
            Authenticated = true
        };
        using var host = CreateTestHost(
            spotifyClient,
            appSettingsJson: BuildAppSettingsJson(
                "http://127.0.0.1:5292",
                enableSmartTrackStart: false,
                roundStartAction: "spotify.profile",
                deathAction: "spotify.control_profile"),
            seedContentRoot: tempRoot =>
            {
                File.WriteAllText(
                    Path.Combine(tempRoot, "profiles.json"),
                    """
                    {
                      "ActiveProfileId": "default",
                      "Profiles": [
                        {
                          "Id": "default",
                          "Name": "Default",
                          "Rules": [
                            {
                              "EventKey": "round_start",
                              "Tracks": [ "spotify:track:anthem" ]
                            }
                          ]
                        }
                      ]
                    }
                    """);

                File.WriteAllText(
                    Path.Combine(tempRoot, "smart-track-starts.json"),
                    """
                    {
                      "Entries": [
                        {
                          "TrackUri": "spotify:track:anthem",
                          "StartPositionMs": 42000
                        }
                      ]
                    }
                    """);
            });

        await host.Client.PostAsJsonAsync("/gsi", CreatePayload(1000, 100, round: 4, phase: "freezetime"));
        var response = await host.Client.PostAsJsonAsync("/gsi", CreatePayload(1001, 100, round: 4, phase: "live"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        spotifyClient.PlayedUris.Should().ContainSingle().Which.Should().Be("spotify:track:anthem");
        spotifyClient.PlayedPositions.Should().ContainSingle().Which.Should().BeNull();
    }

    [Fact]
    public async Task Cs2SetupStatus_ReflectsAutoInstalledConfigUsingConfiguredUri()
    {
        const string gsiBaseUrl = "http://127.0.0.1:6875";
        using var host = CreateTestHost(new FakeSpotifyClient(), gsiBaseUrl);

        var status = await host.Client.GetFromJsonAsync<Cs2SetupStatus>("/setup/cs2/status");

        status.Should().NotBeNull();
        status!.IsCs2Found.Should().BeTrue();
        status.IsCfgInstalled.Should().BeTrue();
        status.IsCfgCurrent.Should().BeTrue();
        status.IsReady.Should().BeTrue();
        status.GsiUri.Should().Be($"{gsiBaseUrl}/gsi");

        var cfgPath = Path.Combine(host.Cs2Root, "game", "csgo", "cfg", "gamestate_integration_undefaultit.cfg");
        File.Exists(cfgPath).Should().BeTrue();
        var cfgContent = await File.ReadAllTextAsync(cfgPath);
        cfgContent.Should().Contain($"{gsiBaseUrl}/gsi");
    }

    [Fact]
    public async Task PostGsiReset_ReturnsNoContent_AndClearsState()
    {
        using var host = CreateTestHost(new FakeSpotifyClient());

        var pre1 = await host.Client.PostAsJsonAsync("/gsi", CreatePayload(2000, 100));
        pre1.StatusCode.Should().Be(HttpStatusCode.OK);
        var pre2 = await host.Client.PostAsJsonAsync("/gsi", CreatePayload(2001, 0));
        pre2.StatusCode.Should().Be(HttpStatusCode.OK);
        var preBody = await pre2.Content.ReadAsStringAsync();
        using (var preDoc = JsonDocument.Parse(preBody))
        {
            preDoc.RootElement.GetProperty("events").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        }

        var eventsBeforeReset = await host.Client.GetStringAsync("/events");
        using (var evDoc = JsonDocument.Parse(eventsBeforeReset))
        {
            evDoc.RootElement.GetArrayLength().Should().BeGreaterThan(0);
        }

        var resetResponse = await host.Client.PostAsync("/gsi/reset", content: null);
        resetResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var eventsAfterReset = await host.Client.GetStringAsync("/events");
        using (var evDoc2 = JsonDocument.Parse(eventsAfterReset))
        {
            evDoc2.RootElement.GetArrayLength().Should().Be(0);
        }

        var post1 = await host.Client.PostAsJsonAsync("/gsi", CreatePayload(3000, 100));
        post1.StatusCode.Should().Be(HttpStatusCode.OK);
        var post1Body = await post1.Content.ReadAsStringAsync();
        using (var post1Doc = JsonDocument.Parse(post1Body))
        {
            post1Doc.RootElement.GetProperty("events").GetInt32().Should().Be(0);
        }

        var post2 = await host.Client.PostAsJsonAsync("/gsi", CreatePayload(3001, 0));
        post2.StatusCode.Should().Be(HttpStatusCode.OK);
        var post2Body = await post2.Content.ReadAsStringAsync();
        using var post2Doc = JsonDocument.Parse(post2Body);
        post2Doc.RootElement.GetProperty("events").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task PostGsiReset_ReturnsForbidden_WhenAllowResetIsFalse()
    {
        using var host = CreateTestHost(
            new FakeSpotifyClient(),
            appSettingsJson: BuildAppSettingsJson("http://127.0.0.1:5292", allowGsiReset: false));

        var response = await host.Client.PostAsync("/gsi/reset", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Cs2Simulator_TSideRound_ViaHttpTransport_SurfacesRoundStartOnEvents()
    {
        using var host = CreateTestHost(new FakeSpotifyClient());
        EnsureClientBaseAddressHasTrailingSlash(host.Client);

        var transport = new HttpGsiTransport(host.Client, NullLogger<HttpGsiTransport>.Instance);
        var runner = new ScenarioRunner(transport, new NullStepGate(), NullLogger<ScenarioRunner>.Instance);
        await runner.RunAsync(
            new TSideRoundScenario(),
            new ScenarioRunOptions { ResetBeforeRun = true, Speed = Speed.Max, VerboseLogging = false },
            CancellationToken.None);

        var body = await host.Client.GetStringAsync("/events");
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.EnumerateArray().Count(e =>
                string.Equals(
                    TryGetStringIgnoreCase(e, "eventKey"),
                    "round_start",
                    StringComparison.Ordinal))
            .Should().Be(1);
    }

    [Fact]
    public async Task DefaultControlProfile_DucksOnRoundStart_AndRestoresOnDeath()
    {
        var spotifyClient = new FakeSpotifyClient
        {
            Authenticated = true,
            CurrentPlayback = new PlaybackState(
                IsPlaying: true,
                VolumePercent: 61,
                Track: null,
                DeviceId: "device",
                DeviceName: "Desktop")
        };
        using var host = CreateTestHost(spotifyClient);

        await host.Client.PostAsJsonAsync("/gsi", CreatePayload(1000, 100, round: 4, phase: "freezetime"));
        var roundStartResponse = await host.Client.PostAsJsonAsync("/gsi", CreatePayload(1001, 100, round: 4, phase: "live"));
        roundStartResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deathResponse = await host.Client.PostAsJsonAsync("/gsi", CreatePayload(1002, 0, round: 4, phase: "live"));
        deathResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        File.Exists(Path.Combine(host.TempRoot, "control-profiles.json")).Should().BeTrue();
        spotifyClient.VolumeCalls.Should().Equal(0, 61);
    }

    [Fact]
    public async Task UserActionEndpoint_RecordsIntentWithCurrentGameContext_AndAppliesControlProfile()
    {
        var spotifyClient = new FakeSpotifyClient
        {
            Authenticated = true,
            CurrentPlayback = new PlaybackState(
                IsPlaying: true,
                VolumePercent: 70,
                Track: null,
                DeviceId: "device",
                DeviceName: "Desktop")
        };
        using var host = CreateTestHost(spotifyClient);

        await host.Client.PostAsJsonAsync("/gsi", CreatePayload(4000, 100, round: 4, phase: "freezetime"));
        await host.Client.PostAsJsonAsync("/gsi", CreatePayload(4001, 100, round: 4, phase: "live"));

        var response = await host.Client.PostAsJsonAsync(
            "/user-actions",
            new { eventKey = "custom:music_pause", action = "pause", detail = "test command" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        spotifyClient.PauseCalls.Should().Be(1);

        var timelineJson = await host.Client.GetStringAsync("/timeline");
        using var doc = JsonDocument.Parse(timelineJson);
        var entries = doc.RootElement.EnumerateArray().ToList();
        entries.Should().HaveCountGreaterThanOrEqualTo(2);
        entries.Select(e => e.GetProperty("source").GetString()).Should().Contain("gsi");

        var userEntry = entries.Last(e => e.GetProperty("source").GetString() == "user_action");
        userEntry.GetProperty("eventKey").GetString().Should().Be("custom:music_pause");
        userEntry.GetProperty("outcome").GetProperty("status").GetString().Should().Be("applied");

        var context = userEntry.GetProperty("gameContext");
        context.GetProperty("isAlive").GetBoolean().Should().BeTrue();
        context.GetProperty("health").GetInt32().Should().Be(100);
        context.GetProperty("round").GetInt32().Should().Be(4);
        context.GetProperty("roundPhase").GetString().Should().Be("live");
        context.GetProperty("recentEventKeys").EnumerateArray()
            .Select(e => e.GetString())
            .Should()
            .Contain("round_start");

        entries.Select(e => e.GetProperty("sequence").GetInt64())
            .Should()
            .BeInAscendingOrder();
    }

    [Fact]
    public async Task UserActionEndpoint_RecordsNoMatchingRuleOutcome()
    {
        using var host = CreateTestHost(new FakeSpotifyClient { Authenticated = true });

        await host.Client.PostAsJsonAsync("/gsi", CreatePayload(4100, 100, round: 5, phase: "live"));
        var response = await host.Client.PostAsJsonAsync(
            "/user-actions",
            new { eventKey = "custom:not_configured", action = "pause" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var responseDoc = JsonDocument.Parse(body);
        responseDoc.RootElement
            .GetProperty("outcome")
            .GetProperty("status")
            .GetString()
            .Should()
            .Be("no_matching_rule");

        var timelineJson = await host.Client.GetStringAsync("/timeline");
        using var timelineDoc = JsonDocument.Parse(timelineJson);
        timelineDoc.RootElement.EnumerateArray()
            .Last()
            .GetProperty("outcome")
            .GetProperty("status")
            .GetString()
            .Should()
            .Be("no_matching_rule");
    }

    [Fact]
    public async Task GsiReset_ClearsTimelineEntries()
    {
        using var host = CreateTestHost(new FakeSpotifyClient { Authenticated = true });

        await host.Client.PostAsJsonAsync("/gsi", CreatePayload(4200, 100, round: 6, phase: "freezetime"));
        await host.Client.PostAsJsonAsync("/gsi", CreatePayload(4201, 100, round: 6, phase: "live"));
        await host.Client.PostAsJsonAsync("/user-actions", new { eventKey = "custom:music_pause", action = "pause" });

        var before = await host.Client.GetStringAsync("/timeline");
        using (var beforeDoc = JsonDocument.Parse(before))
        {
            beforeDoc.RootElement.GetArrayLength().Should().BeGreaterThan(0);
        }

        var resetResponse = await host.Client.PostAsync("/gsi/reset", content: null);
        resetResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await host.Client.GetStringAsync("/timeline");
        using var afterDoc = JsonDocument.Parse(after);
        afterDoc.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task TimelineEpisodes_ExposeManualIntentWindows()
    {
        using var host = CreateTestHost(new FakeSpotifyClient { Authenticated = true });

        await host.Client.PostAsJsonAsync("/gsi", CreatePayload(4300, 100, round: 7, phase: "freezetime"));
        await host.Client.PostAsJsonAsync("/gsi", CreatePayload(4301, 100, round: 7, phase: "live"));
        await host.Client.PostAsJsonAsync("/user-actions", new { eventKey = "custom:music_pause", action = "pause" });
        await host.Client.PostAsJsonAsync("/gsi", CreatePayload(4302, 0, round: 7, phase: "live"));

        var episodesJson = await host.Client.GetStringAsync("/timeline/episodes");
        using var doc = JsonDocument.Parse(episodesJson);
        doc.RootElement.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        var episode = doc.RootElement.EnumerateArray().First();
        episode.GetProperty("label").GetProperty("eventKey").GetString().Should().Be("custom:music_pause");
        episode.GetProperty("before").GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
        episode.GetProperty("after").GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Cs2GameAdapter_PreservesGsiSnapshotMapperOutput()
    {
        var mapper = CreateSnapshotMapper();
        var adapter = new Cs2GameAdapter(mapper);
        var payload = CreatePayloadDto(1200, 100, round: 9, phase: "live");
        var receivedAt = DateTimeOffset.UnixEpoch.AddSeconds(1200);

        var mapped = mapper.Map(payload, receivedAt);
        var observed = adapter.Adapt(payload, receivedAt);

        observed.Raw.Should().BeEquivalentTo(mapped);
    }

    private static object CreatePayload(long timestamp, int health, int? round = null, string? phase = null)
    {
        return new
        {
            provider = new { timestamp },
            map = new { matchid = "match", round, phase },
            player = new
            {
                steamid = "player",
                activity = "playing",
                state = new { health, armor = 0 }
            }
        };
    }

    private static GsiPayloadDto CreatePayloadDto(long timestamp, int health, int? round = null, string? phase = null)
    {
        return new GsiPayloadDto
        {
            Provider = new ProviderDto { Timestamp = timestamp },
            Map = new MapDto
            {
                MatchId = "match",
                Round = round,
                Phase = phase
            },
            Player = new PlayerDto
            {
                SteamId = "player",
                Activity = "playing",
                State = new PlayerStateDto { Health = health, Armor = 0 }
            },
        };
    }

    private static GsiSnapshotMapper CreateSnapshotMapper()
    {
        return new GsiSnapshotMapper(new ISnapshotModuleMapper[]
        {
            new RoundModuleMapper(),
            new VitalsModuleMapper(),
            new PositionModuleMapper(),
            new CombatModuleMapper()
        });
    }

    private sealed class FakeSpotifyClient : ISpotifyClient
    {
        public bool Authenticated { get; set; }
        public PlaybackState? CurrentPlayback { get; set; }
        public List<string?> PlayedUris { get; } = new();
        public List<int?> PlayedPositions { get; } = new();
        public List<int> VolumeCalls { get; } = new();
        public int PauseCalls { get; private set; }
        public int ResumeCalls { get; private set; }

        public Task<PlaybackState?> GetCurrentPlaybackAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CurrentPlayback);
        }

        public Task PlayAsync(string? uri = null, CancellationToken cancellationToken = default)
        {
            return PlayAsync(uri, positionMs: null, cancellationToken);
        }

        public Task PlayAsync(string? uri = null, int? positionMs = null, CancellationToken cancellationToken = default)
        {
            PlayedUris.Add(uri);
            PlayedPositions.Add(positionMs);
            return Task.CompletedTask;
        }
        public Task PauseAsync(CancellationToken cancellationToken = default)
        {
            PauseCalls++;
            CurrentPlayback = CurrentPlayback is null
                ? null
                : CurrentPlayback with { IsPlaying = false };
            return Task.CompletedTask;
        }

        public Task ResumeAsync(CancellationToken cancellationToken = default)
        {
            ResumeCalls++;
            CurrentPlayback = CurrentPlayback is null
                ? null
                : CurrentPlayback with { IsPlaying = true };
            return Task.CompletedTask;
        }
        public Task SetVolumeAsync(int volume, CancellationToken cancellationToken = default)
        {
            VolumeCalls.Add(volume);
            CurrentPlayback = CurrentPlayback is null
                ? null
                : CurrentPlayback with { VolumePercent = volume };
            return Task.CompletedTask;
        }

        public Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default) => Task.FromResult(Authenticated);
        public Task<string> GetAuthorizationUrlAsync(string state, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public Task<SpotifyAuthResult> AuthenticateAsync(string authorizationCode, CancellationToken cancellationToken = default)
            => Task.FromResult(new SpotifyAuthResult(string.Empty, string.Empty, DateTimeOffset.UtcNow, Array.Empty<string>()));
    }

    private sealed class ThrowingSpotifyClient : ISpotifyClient
    {
        public Task<PlaybackState?> GetCurrentPlaybackAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Spotify unavailable");

        public Task PlayAsync(string? uri = null, CancellationToken cancellationToken = default)
            => PlayAsync(uri, positionMs: null, cancellationToken);

        public Task PlayAsync(string? uri = null, int? positionMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetVolumeAsync(int volume, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string> GetAuthorizationUrlAsync(string state, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public Task<SpotifyAuthResult> AuthenticateAsync(string authorizationCode, CancellationToken cancellationToken = default)
            => Task.FromResult(new SpotifyAuthResult(string.Empty, string.Empty, DateTimeOffset.UtcNow, Array.Empty<string>()));
    }

    private TestHostContext CreateTestHost(
        ISpotifyClient spotifyClient,
        string gsiBaseUrl = "http://127.0.0.1:5292",
        string? appSettingsJson = null,
        Action<string>? seedContentRoot = null)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "UndefaultIt.Tests", Guid.NewGuid().ToString("N"));
        var cs2Root = Path.Combine(tempRoot, "Counter-Strike Global Offensive");
        Directory.CreateDirectory(Path.Combine(cs2Root, "game", "csgo", "cfg"));
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "appsettings.json"), appSettingsJson ?? BuildAppSettingsJson(gsiBaseUrl));
        seedContentRoot?.Invoke(tempRoot);

        var previousOverride = Environment.GetEnvironmentVariable("UNDEFAULTIT_CS2_PATH");
        Environment.SetEnvironmentVariable("UNDEFAULTIT_CS2_PATH", cs2Root);

        var customizedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.ContentRootKey, tempRoot);
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ISpotifyClient>(_ => spotifyClient);
            });
        });

        return new TestHostContext(
            customizedFactory,
            customizedFactory.CreateClient(),
            tempRoot,
            cs2Root,
            previousOverride);
    }

    private static string? TryGetStringIgnoreCase(JsonElement element, string propertyName)
    {
        foreach (var p in element.EnumerateObject())
        {
            if (string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return p.Value.GetString();
            }
        }

        return null;
    }

    private static void EnsureClientBaseAddressHasTrailingSlash(HttpClient client)
    {
        var baseUri = client.BaseAddress;
        if (baseUri is null)
        {
            return;
        }

        var s = baseUri.ToString();
        if (!s.EndsWith('/'))
        {
            client.BaseAddress = new Uri(s + "/");
        }
    }

    private static string BuildAppSettingsJson(
        string gsiBaseUrl,
        bool enableSmartTrackStart = false,
        string roundStartAction = "spotify.control_profile",
        string deathAction = "spotify.control_profile",
        bool allowGsiReset = true)
    {
        return $$"""
        {
          "Logging": {
            "LogLevel": {
              "Default": "Information",
              "Microsoft.AspNetCore": "Warning"
            }
          },
          "AllowedHosts": "*",
          "Spotify": {
            "ClientId": "",
            "ClientSecret": "",
            "RedirectUri": "http://127.0.0.1:5292/callback",
            "Scopes": [
              "user-modify-playback-state",
              "user-read-playback-state"
            ]
          },
          "Gsi": {
            "Method": "POST",
            "Path": "/gsi",
            "Url": "{{gsiBaseUrl}}",
            "AllowReset": {{(allowGsiReset ? "true" : "false")}}
          },
          "UseMockSpotify": true,
          "EventDetector": {
            "EnableRoundStart": true,
            "EnableDeath": true,
            "EnableCombat": false,
            "EnableIdle": false,
            "RoundStartPhase": "live",
            "DeathCooldown": "00:00:01"
          },
          "SpotifyVolumeDuck": {
            "MuteVolume": 0,
            "FallbackRestoreVolume": 50
          },
          "SmartTrackStart": {
            "Enabled": {{(enableSmartTrackStart ? "true" : "false")}},
            "PreloadOnStartup": true
          },
          "RulesEngine": {
            "ActionMap": {
              "round_start": [ "{{roundStartAction}}" ],
              "death": [ "{{deathAction}}" ]
            }
          }
        }
        """;
    }

    private sealed class NullStepGate : IStepGate
    {
        public Task WaitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestHostContext : IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly string? _previousOverride;

        public TestHostContext(
            WebApplicationFactory<Program> factory,
            HttpClient client,
            string tempRoot,
            string cs2Root,
            string? previousOverride)
        {
            _factory = factory;
            Client = client;
            TempRoot = tempRoot;
            Cs2Root = cs2Root;
            _previousOverride = previousOverride;
        }

        public HttpClient Client { get; }

        public WebApplicationFactory<Program> Factory => _factory;

        public string TempRoot { get; }

        public string Cs2Root { get; }

        public void Dispose()
        {
            Client.Dispose();
            _factory.Dispose();
            Environment.SetEnvironmentVariable("UNDEFAULTIT_CS2_PATH", _previousOverride);

            if (Directory.Exists(TempRoot))
            {
                Directory.Delete(TempRoot, recursive: true);
            }
        }
    }
}
