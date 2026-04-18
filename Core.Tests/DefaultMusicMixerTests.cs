using Core.Music;
using FluentAssertions;

namespace Core.Tests;

public sealed class DefaultMusicMixerTests
{
    private readonly DefaultMusicMixer _mixer = new();

    [Fact]
    public void Merge_Safe_MultipliesGainsAndClamps()
    {
        var intents = new IAudioIntent[]
        {
            new GainAudioIntent("defuse", priority: 1, gain: 0.5f),
            new GainAudioIntent("freeze", priority: 1, gain: 0.8f)
        };
        var ctx = new MusicMixerContext(
            SafetyState: MusicSafetyState.Safe,
            BaseVolumePercent: 100,
            FloorVolumePercent: 10,
            CeilingVolumePercent: 100,
            ForbidFloorInDanger: true);

        var result = _mixer.Merge(intents, ctx);

        result.TargetVolumePercent.Should().Be(40);
        result.HardSuppressAudio.Should().BeFalse();
    }

    [Fact]
    public void Merge_Danger_ForbidsFloor_Suppresses()
    {
        var ctx = new MusicMixerContext(
            SafetyState: MusicSafetyState.Danger,
            BaseVolumePercent: 80,
            FloorVolumePercent: 5,
            CeilingVolumePercent: 100,
            ForbidFloorInDanger: true);

        var result = _mixer.Merge(Array.Empty<IAudioIntent>(), ctx);

        result.TargetVolumePercent.Should().Be(0);
        result.HardSuppressAudio.Should().BeTrue();
    }

    [Fact]
    public void Merge_Danger_AllowsFloor_WhenNotForbidden()
    {
        var ctx = new MusicMixerContext(
            SafetyState: MusicSafetyState.Danger,
            BaseVolumePercent: 80,
            FloorVolumePercent: 7,
            CeilingVolumePercent: 100,
            ForbidFloorInDanger: false);

        var result = _mixer.Merge(Array.Empty<IAudioIntent>(), ctx);

        result.TargetVolumePercent.Should().Be(7);
        result.HardSuppressAudio.Should().BeTrue();
    }

    [Fact]
    public void Merge_Unknown_UsesFloorOrZero()
    {
        var ctx = new MusicMixerContext(
            SafetyState: MusicSafetyState.Unknown,
            BaseVolumePercent: 80,
            FloorVolumePercent: 12,
            CeilingVolumePercent: 100,
            ForbidFloorInDanger: true);

        var result = _mixer.Merge(Array.Empty<IAudioIntent>(), ctx);

        result.TargetVolumePercent.Should().Be(12);
    }
}
