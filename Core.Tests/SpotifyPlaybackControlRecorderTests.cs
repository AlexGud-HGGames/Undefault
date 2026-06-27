using Core.Configuration;
using Core.Actions.Spotify;
using Core.Spotify;
using Core.Spotify.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Core.Tests;

/// <summary>
/// Unit tests for the <see cref="IPlaybackEventRecorder"/> seam in <see cref="SpotifyPlaybackControlCoordinator"/>.
/// Verifies the recorder is invoked only after a confirmed, state-changing pause/resume and never on no-ops,
/// auth/device failures, or exceptions.
/// </summary>
public class SpotifyPlaybackControlRecorderTests
{
    [Fact]
    public async Task Pause_WhenPlaying_RecordsPausedTransitionOnce()
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
        recorder.PausedCalls.Should().ContainSingle();
        recorder.ResumedCalls.Should().BeEmpty();
        recorder.PausedCalls[0].Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Resume_WhenPaused_RecordsResumedTransitionOnce()
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
        recorder.ResumedCalls.Should().ContainSingle();
        recorder.PausedCalls.Should().BeEmpty();
        recorder.ResumedCalls[0].Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
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
    public async Task RecorderFailure_DoesNotBreakPause()
    {
        var client = new FakeSpotifyClient
        {
            Authenticated = true,
            CurrentPlayback = Playing()
        };
        var recorder = new ThrowingPlaybackEventRecorder();
        var coordinator = BuildCoordinator(client, recorder);

        var act = async () => await coordinator.TryPauseAsync("custom:music_pause");

        await act.Should().NotThrowAsync();
        client.PauseCalls.Should().Be(1);
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

    private sealed class ThrowingPlaybackEventRecorder : IPlaybackEventRecorder
    {
        public Task RecordPausedAsync(DateTimeOffset timestampUtc, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("timeline unavailable");

        public Task RecordResumedAsync(DateTimeOffset timestampUtc, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("timeline unavailable");
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
