using Core.Configuration;
using Core.Actions.Spotify;
using Core.Spotify;
using Core.Spotify.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Core.Tests;

/// <summary>
/// Unit tests for <see cref="SpotifyPlaybackControlCoordinator"/> pause/resume control behavior.
/// UND-77 moved pause/resume transition recording to <c>PlaybackStateObserver</c>; the coordinator no
/// longer records, so these tests verify playback control (pause/resume applied once on a state change)
/// and that no recorder calls are made on no-ops, auth/device failures, or when no recorder is supplied.
/// </summary>
public class SpotifyPlaybackControlRecorderTests
{
    [Fact]
    public async Task Pause_WhenPlaying_PausesOnce_WithoutRecordingTransition()
    {
        var client = new FakeSpotifyClient
        {
            Authenticated = true,
            CurrentPlayback = Playing()
        };
        var recorder = new CapturePlaybackEventRecorder();
        var coordinator = BuildCoordinator(client, recorder);

        await coordinator.TryPauseAsync("custom:music_pause");

        client.PauseCalls.Should().Be(1);
        recorder.PausedCalls.Should().BeEmpty("UND-77 moved recording to the playback state observer");
        recorder.ResumedCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Resume_WhenPaused_ResumesOnce_WithoutRecordingTransition()
    {
        var client = new FakeSpotifyClient
        {
            Authenticated = true,
            CurrentPlayback = Paused()
        };
        var recorder = new CapturePlaybackEventRecorder();
        var coordinator = BuildCoordinator(client, recorder);

        await coordinator.TryResumeAsync("custom:music_resume");

        client.ResumeCalls.Should().Be(1);
        recorder.ResumedCalls.Should().BeEmpty("UND-77 moved recording to the playback state observer");
        recorder.PausedCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Pause_WhenAlreadyPaused_DoesNotRecordTransition()
    {
        var client = new FakeSpotifyClient
        {
            Authenticated = true,
            CurrentPlayback = Paused()
        };
        var recorder = new CapturePlaybackEventRecorder();
        var coordinator = BuildCoordinator(client, recorder);

        await coordinator.TryPauseAsync("custom:music_pause");

        client.PauseCalls.Should().Be(0);
        recorder.PausedCalls.Should().BeEmpty();
        recorder.ResumedCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Resume_WhenAlreadyPlaying_DoesNotRecordTransition()
    {
        var client = new FakeSpotifyClient
        {
            Authenticated = true,
            CurrentPlayback = Playing()
        };
        var recorder = new CapturePlaybackEventRecorder();
        var coordinator = BuildCoordinator(client, recorder);

        await coordinator.TryResumeAsync("custom:music_resume");

        client.ResumeCalls.Should().Be(0);
        recorder.PausedCalls.Should().BeEmpty();
        recorder.ResumedCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Pause_WhenNotAuthenticated_DoesNotRecordTransition()
    {
        var client = new FakeSpotifyClient
        {
            Authenticated = false,
            CurrentPlayback = Playing()
        };
        var recorder = new CapturePlaybackEventRecorder();
        var coordinator = BuildCoordinator(client, recorder);

        await coordinator.TryPauseAsync("custom:music_pause");

        client.PauseCalls.Should().Be(0);
        recorder.PausedCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Pause_WhenNoPlaybackDevice_DoesNotRecordTransition()
    {
        var client = new FakeSpotifyClient
        {
            Authenticated = true,
            CurrentPlayback = null
        };
        var recorder = new CapturePlaybackEventRecorder();
        var coordinator = BuildCoordinator(client, recorder);

        await coordinator.TryPauseAsync("custom:music_pause");

        client.PauseCalls.Should().Be(0);
        recorder.PausedCalls.Should().BeEmpty();
    }

    [Fact]
    public void NullRecorder_DefaultsToNoOp_AndDoesNotThrow()
    {
        var client = new FakeSpotifyClient
        {
            Authenticated = true,
            CurrentPlayback = Playing()
        };

        // 3-arg constructor (no recorder) must remain supported for existing callers/tests.
        var coordinator = new SpotifyPlaybackControlCoordinator(
            client,
            Options.Create(new SpotifyVolumeDuckOptions()),
            NullLogger<SpotifyPlaybackControlCoordinator>.Instance);

        Func<Task> act = async () => await coordinator.TryPauseAsync("custom:music_pause");
        act.Should().NotThrowAsync();
    }

    private static SpotifyPlaybackControlCoordinator BuildCoordinator(
        ISpotifyClient client,
        IPlaybackEventRecorder recorder)
    {
        return new SpotifyPlaybackControlCoordinator(
            client,
            Options.Create(new SpotifyVolumeDuckOptions
            {
                MuteVolume = 0,
                FallbackRestoreVolume = 50
            }),
            recorder,
            NullLogger<SpotifyPlaybackControlCoordinator>.Instance);
    }

    private static PlaybackState Playing() => new(
        IsPlaying: true,
        VolumePercent: 70,
        Track: null,
        DeviceId: "device",
        DeviceName: "Desktop");

    private static PlaybackState Paused() => new(
        IsPlaying: false,
        VolumePercent: 70,
        Track: null,
        DeviceId: "device",
        DeviceName: "Desktop");

    private sealed class CapturePlaybackEventRecorder : IPlaybackEventRecorder
    {
        public List<DateTimeOffset> PausedCalls { get; } = new();
        public List<DateTimeOffset> ResumedCalls { get; } = new();

        public Task RecordPausedAsync(DateTimeOffset timestampUtc, CancellationToken cancellationToken = default)
        {
            PausedCalls.Add(timestampUtc);
            return Task.CompletedTask;
        }

        public Task RecordResumedAsync(DateTimeOffset timestampUtc, CancellationToken cancellationToken = default)
        {
            ResumedCalls.Add(timestampUtc);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSpotifyClient : ISpotifyClient
    {
        public bool Authenticated { get; set; }
        public PlaybackState? CurrentPlayback { get; set; }
        public int PauseCalls { get; private set; }
        public int ResumeCalls { get; private set; }

        public Task<PlaybackState?> GetCurrentPlaybackAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CurrentPlayback);

        public Task PlayAsync(string? uri = null, int? positionMs = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

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
            => Task.CompletedTask;

        public Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Authenticated);

        public Task<string> GetAuthorizationUrlAsync(string state, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<SpotifyAuthResult> AuthenticateAsync(string authorizationCode, string state, CancellationToken cancellationToken = default)
            => Task.FromResult(new SpotifyAuthResult(string.Empty, string.Empty, DateTimeOffset.UtcNow, Array.Empty<string>()));
    }
}
