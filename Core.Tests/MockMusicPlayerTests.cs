using Core.Music;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Tests;

public class MockMusicPlayerTests
{
    [Fact]
    public async Task PlayPauseResume_UpdateStateAndCallCounts()
    {
        var player = new MockMusicPlayer(NullLogger<MockMusicPlayer>.Instance);

        await player.PlayAsync();
        var playing = await player.GetStateAsync();
        playing!.Status.Should().Be(PlaybackStatus.Playing);
        player.PlayCalls.Should().Be(1);

        await player.PauseAsync();
        var paused = await player.GetStateAsync();
        paused!.Status.Should().Be(PlaybackStatus.Paused);
        player.PauseCalls.Should().Be(1);

        await player.ResumeAsync();
        var resumed = await player.GetStateAsync();
        resumed!.Status.Should().Be(PlaybackStatus.Playing);
        player.ResumeCalls.Should().Be(1);
    }

    [Fact]
    public async Task Resume_WhenAlreadyPlaying_IsIdempotent()
    {
        var player = new MockMusicPlayer(NullLogger<MockMusicPlayer>.Instance);
        player.SeedState(PlaybackStatus.Playing);

        await player.ResumeAsync();
        await player.ResumeAsync();

        var state = await player.GetStateAsync();
        state!.Status.Should().Be(PlaybackStatus.Playing);
        player.ResumeCalls.Should().Be(2);
    }

    [Fact]
    public async Task Unavailable_ReturnsNoState_AndDoesNotChangeTransport()
    {
        var player = new MockMusicPlayer(NullLogger<MockMusicPlayer>.Instance);
        player.SeedState(PlaybackStatus.Playing);
        player.Available = false;

        (await player.IsAvailableAsync()).Should().BeFalse();
        (await player.GetStateAsync()).Should().BeNull();

        await player.PauseAsync();
        player.Available = true;
        var state = await player.GetStateAsync();
        state!.Status.Should().Be(PlaybackStatus.Playing);
    }

    [Fact]
    public async Task NextPreviousAndVolume_AreRecorded()
    {
        var player = new MockMusicPlayer(NullLogger<MockMusicPlayer>.Instance);

        await player.NextAsync();
        await player.PreviousAsync();
        await player.SetVolumeAsync(25);

        player.NextCalls.Should().Be(1);
        player.PreviousCalls.Should().Be(1);
        player.VolumeCalls.Should().Equal(25);
        var state = await player.GetStateAsync();
        state!.VolumePercent.Should().Be(25);
    }
}
