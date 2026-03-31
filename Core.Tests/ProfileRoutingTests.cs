using Core.Actions;
using Core.Actions.Spotify;
using Core.Configuration;
using Core.Diff;
using Core.Models;
using Core.Rules;
using Core.Spotify;
using Core.Spotify.Models;
using Core.Stores;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Core.Tests;

public class ProfileRoutingTests
{
    [Fact]
    public void MusicProfile_FindRule_UsesCaseInsensitiveEventKeys()
    {
        var profile = new MusicProfile(
            "profile-1",
            "Default",
            new List<EventTrackRule>
            {
                new("custom:clutch_1v3", new List<string> { "spotify:track:aaa" })
            });

        var rule = profile.FindRule("CUSTOM:CLUTCH_1V3");

        rule.Should().NotBeNull();
        rule!.Tracks.Should().ContainSingle().Which.Should().Be("spotify:track:aaa");
    }

    [Fact]
    public void ConsoleControlProfile_FindRule_UsesCaseInsensitiveEventKeys()
    {
        var profile = new ConsoleControlProfile(
            "console-default",
            "Console Default",
            new List<EventControlRule>
            {
                new("custom:music_off", MusicControlCommands.Pause)
            });

        var rule = profile.FindRule("CUSTOM:MUSIC_OFF");

        rule.Should().NotBeNull();
        rule!.Command.Should().Be(MusicControlCommands.Pause);
    }

    [Fact]
    public async Task RulesEngine_RoutesActions_UsingCanonicalEventKey()
    {
        var action = new CaptureAction();
        var engine = new RulesEngine(
            new TestSnapshotStore(),
            new SnapshotDiffer(),
            new EventDetector(new EventDetectorOptions
            {
                DeathCooldown = TimeSpan.Zero,
                CombatDebounce = TimeSpan.Zero,
                IdleDebounce = TimeSpan.FromMinutes(1)
            }),
            new[] { action },
            Options.Create(new RulesEngineOptions
            {
                ActionMap = new Dictionary<string, List<string>>
                {
                    [EventKeys.Death] = new() { action.Key }
                }
            }));

        await engine.EvaluateAsync(BuildSnapshot(DateTimeOffset.UtcNow, 100, isAlive: true));
        await engine.EvaluateAsync(BuildSnapshot(DateTimeOffset.UtcNow.AddSeconds(1), 0, isAlive: false));

        action.Events.Should().ContainSingle();
        action.Events[0].EventKey.Should().Be(EventKeys.Death);
    }

    [Fact]
    public async Task SpotifyProfileAction_PlaysTrackForResolvedEventKey()
    {
        var spotifyClient = new FakeSpotifyClient();
        var playbackPolicy = new FakePlaybackPolicy();
        var profileService = new FakeProfileService(new MusicProfilesConfig(
            "default",
            new List<MusicProfile>
            {
                new("default", "Default", new List<EventTrackRule>
                {
                    new(EventKeys.Death, new List<string> { "spotify:track:death-song" })
                })
            }));
        var action = new SpotifyProfileAction(
            spotifyClient,
            profileService,
            playbackPolicy,
            NullLogger<SpotifyProfileAction>.Instance);

        await action.ExecuteAsync(NormalizedEvent.Death(BuildSnapshot(DateTimeOffset.UtcNow, 0, isAlive: false)));

        spotifyClient.PlayedUris.Should().ContainSingle().Which.Should().Be("spotify:track:death-song");
        playbackPolicy.BeforePlayCalls.Should().ContainSingle().Which.EventKey.Should().Be(EventKeys.Death);
        spotifyClient.PauseCalls.Should().Be(0);
        spotifyClient.ResumeCalls.Should().Be(0);
        spotifyClient.VolumeCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task SpotifyControlProfileAction_DucksOnRoundStart_AndRestoresOnDeath()
    {
        var spotifyClient = new FakeSpotifyClient
        {
            CurrentPlayback = new PlaybackState(
                IsPlaying: true,
                VolumePercent: 72,
                Track: null,
                DeviceId: "device",
                DeviceName: "Desktop")
        };
        var controlProfileService = new FakeControlProfileService(new ConsoleControlProfilesConfig(
            "console-default",
            new List<ConsoleControlProfile>
            {
                new("console-default", "Console Default", new List<EventControlRule>
                {
                    new(EventKeys.RoundStart, MusicControlCommands.Duck, 10),
                    new(EventKeys.Death, MusicControlCommands.RestoreVolume)
                })
            }));
        var action = new SpotifyControlProfileAction(
            spotifyClient,
            controlProfileService,
            Options.Create(new SpotifyVolumeDuckOptions
            {
                MuteVolume = 0,
                FallbackRestoreVolume = 50
            }),
            NullLogger<SpotifyControlProfileAction>.Instance);

        await action.ExecuteAsync(NormalizedEvent.RoundStart(BuildSnapshot(DateTimeOffset.UtcNow, 100, isAlive: true)));
        await action.ExecuteAsync(NormalizedEvent.Death(BuildSnapshot(DateTimeOffset.UtcNow.AddSeconds(1), 0, isAlive: false)));

        spotifyClient.VolumeCalls.Should().Equal(10, 72);
    }

    [Fact]
    public async Task SpotifyControlProfileAction_PausesAndResumesWhenConfigured()
    {
        var spotifyClient = new FakeSpotifyClient
        {
            CurrentPlayback = new PlaybackState(
                IsPlaying: true,
                VolumePercent: 55,
                Track: null,
                DeviceId: "device",
                DeviceName: "Desktop")
        };
        var controlProfileService = new FakeControlProfileService(new ConsoleControlProfilesConfig(
            "console-default",
            new List<ConsoleControlProfile>
            {
                new("console-default", "Console Default", new List<EventControlRule>
                {
                    new(EventKeys.RoundStart, MusicControlCommands.Pause),
                    new(EventKeys.Death, MusicControlCommands.Resume)
                })
            }));
        var action = new SpotifyControlProfileAction(
            spotifyClient,
            controlProfileService,
            Options.Create(new SpotifyVolumeDuckOptions()),
            NullLogger<SpotifyControlProfileAction>.Instance);

        await action.ExecuteAsync(NormalizedEvent.RoundStart(BuildSnapshot(DateTimeOffset.UtcNow, 100, isAlive: true)));
        await action.ExecuteAsync(NormalizedEvent.Death(BuildSnapshot(DateTimeOffset.UtcNow.AddSeconds(1), 0, isAlive: false)));

        spotifyClient.PauseCalls.Should().Be(1);
        spotifyClient.ResumeCalls.Should().Be(1);
    }

    [Fact]
    public async Task SpotifyVolumeDuckAction_MutesOnRoundStart_AndRestoresOnDeath()
    {
        var spotifyClient = new FakeSpotifyClient
        {
            CurrentPlayback = new PlaybackState(
                IsPlaying: true,
                VolumePercent: 72,
                Track: null,
                DeviceId: "device",
                DeviceName: "Desktop")
        };
        var action = new SpotifyVolumeDuckAction(
            spotifyClient,
            Options.Create(new SpotifyVolumeDuckOptions
            {
                MuteVolume = 0,
                FallbackRestoreVolume = 50
            }),
            NullLogger<SpotifyVolumeDuckAction>.Instance);

        await action.ExecuteAsync(NormalizedEvent.RoundStart(BuildSnapshot(DateTimeOffset.UtcNow, 100, isAlive: true), "round=3"));
        await action.ExecuteAsync(NormalizedEvent.Death(BuildSnapshot(DateTimeOffset.UtcNow.AddSeconds(10), 0, isAlive: false)));

        spotifyClient.VolumeCalls.Should().Equal(0, 72);
    }

    private static GameSnapshot BuildSnapshot(
        DateTimeOffset timestamp,
        int health,
        bool isAlive)
    {
        return new GameSnapshot(
            Timestamp: timestamp,
            GameId: "cs2",
            MatchId: "match",
            PlayerId: "player",
            Modules: new ISnapshotModule[]
            {
                new VitalsModule(Health: health, Armor: 0, IsAlive: isAlive),
                new PositionModule(Position: Vector3.Zero, IsMoving: false),
                new CombatModule(InCombatHint: false, LastDamageDealtAt: null, LastDamageReceivedAt: null)
            });
    }

    private sealed class CaptureAction : IEventAction
    {
        public string Key => "capture";

        public List<NormalizedEvent> Events { get; } = new();

        public Task ExecuteAsync(NormalizedEvent normalizedEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(normalizedEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class TestSnapshotStore : ISnapshotStore
    {
        private GameSnapshot? _last;

        public GameSnapshot? GetLast() => _last;

        public void Save(GameSnapshot snapshot) => _last = snapshot;

        public void Clear() => _last = null;
    }

    private sealed class FakeProfileService : IProfileService
    {
        private MusicProfilesConfig _config;

        public FakeProfileService(MusicProfilesConfig config)
        {
            _config = config;
        }

        public Task<MusicProfilesConfig> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_config);
        }

        public Task SaveAsync(MusicProfilesConfig config, CancellationToken cancellationToken = default)
        {
            _config = config;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeControlProfileService : IControlProfileService
    {
        private ConsoleControlProfilesConfig _config;

        public FakeControlProfileService(ConsoleControlProfilesConfig config)
        {
            _config = config;
        }

        public string FilePath => "control-profiles.json";

        public Task<ConsoleControlProfilesConfig> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_config);
        }

        public Task SaveAsync(ConsoleControlProfilesConfig config, CancellationToken cancellationToken = default)
        {
            _config = config;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePlaybackPolicy : IPlaybackPolicy
    {
        public List<NormalizedEvent> BeforePlayCalls { get; } = new();

        public Task BeforePlayAsync(NormalizedEvent normalizedEvent, CancellationToken cancellationToken = default)
        {
            BeforePlayCalls.Add(normalizedEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSpotifyClient : ISpotifyClient
    {
        public PlaybackState? CurrentPlayback { get; set; }
        public List<string?> PlayedUris { get; } = new();
        public int PauseCalls { get; private set; }
        public int ResumeCalls { get; private set; }
        public List<int> VolumeCalls { get; } = new();

        public Task<PlaybackState?> GetCurrentPlaybackAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CurrentPlayback);
        }

        public Task PlayAsync(string? uri = null, CancellationToken cancellationToken = default)
        {
            PlayedUris.Add(uri);
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

        public Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<string> GetAuthorizationUrlAsync(string state, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(string.Empty);
        }

        public Task<SpotifyAuthResult> AuthenticateAsync(string authorizationCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SpotifyAuthResult(string.Empty, string.Empty, DateTimeOffset.UtcNow, Array.Empty<string>()));
        }
    }
}
