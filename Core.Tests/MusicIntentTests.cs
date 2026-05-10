using Core.Adapters;
using Core.Music;
using FluentAssertions;

namespace Core.Tests;

public sealed class MusicIntentTests
{
    [Fact]
    public void Create_DefaultsAreNoOp()
    {
        var intent = MusicIntent.Create();

        intent.TransportIntent.Should().Be(TransportIntentNeutral.NoChange);
        intent.FloorVolumePercent.Should().BeNull();
        intent.CeilingVolumePercent.Should().BeNull();
        intent.GainBias.Should().BeNull();
        intent.CooldownHint.Should().BeNull();
        intent.Reason.Should().BeEmpty();
        intent.SafetyOverrideAllowed.Should().BeFalse();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_RejectsOutOfRangeFloor(int value)
    {
        Action act = () => MusicIntent.Create(floorVolumePercent: value);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_RejectsOutOfRangeCeiling(int value)
    {
        Action act = () => MusicIntent.Create(ceilingVolumePercent: value);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_RejectsFloorAboveCeilingOnSameIntent()
    {
        Action act = () => MusicIntent.Create(floorVolumePercent: 60, ceilingVolumePercent: 50);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_RejectsNegativeCooldown()
    {
        Action act = () => MusicIntent.Create(cooldownHint: TimeSpan.FromMilliseconds(-1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_RejectsNonFiniteGain()
    {
        Action act = () => MusicIntent.Create(gainBias: float.NaN);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Merge_NullThrows()
    {
        Action act = () => MusicIntent.Merge(null!, MusicSafetyState.Safe);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Merge_EmptyInputReturnsNoOp()
    {
        var merged = MusicIntent.Merge(Array.Empty<MusicIntent>(), MusicSafetyState.Safe);

        merged.Should().Be(MusicIntent.NoOp);
    }

    [Fact]
    public void Merge_DangerOverridesEverything()
    {
        var positive = MusicIntent.Create(
            transportIntent: TransportIntentNeutral.PreferResume,
            floorVolumePercent: 80,
            ceilingVolumePercent: 100,
            gainBias: 0.5f,
            reason: "scenario:resume",
            safetyOverrideAllowed: true);

        var merged = MusicIntent.Merge(new[] { positive }, MusicSafetyState.Danger);

        merged.TransportIntent.Should().Be(TransportIntentNeutral.PreferSilence);
        merged.FloorVolumePercent.Should().Be(0);
        merged.CeilingVolumePercent.Should().Be(0);
        merged.GainBias.Should().BeNull();
        merged.SafetyOverrideAllowed.Should().BeFalse(
            "SafetyOverrideAllowed=true is ignored under Danger; safety always wins");
        merged.Reason.Should().StartWith("danger");
        merged.Reason.Should().Contain("scenario:resume");
    }

    [Fact]
    public void Merge_DangerWithNoIntentsStillProducesSilenceFloorZero()
    {
        var merged = MusicIntent.Merge(Array.Empty<MusicIntent>(), MusicSafetyState.Danger);

        merged.TransportIntent.Should().Be(TransportIntentNeutral.PreferSilence);
        merged.FloorVolumePercent.Should().Be(0);
        merged.CeilingVolumePercent.Should().Be(0);
        merged.Reason.Should().Be("danger");
    }

    [Fact]
    public void Merge_TransportPrecedence_HighestWins()
    {
        var resume = MusicIntent.Create(transportIntent: TransportIntentNeutral.PreferResume);
        var noChange = MusicIntent.Create(transportIntent: TransportIntentNeutral.NoChange);
        var pause = MusicIntent.Create(transportIntent: TransportIntentNeutral.PreferPause);
        var silence = MusicIntent.Create(transportIntent: TransportIntentNeutral.PreferSilence);

        MusicIntent.Merge(new[] { resume, noChange }, MusicSafetyState.Safe).TransportIntent
            .Should().Be(TransportIntentNeutral.PreferResume);
        MusicIntent.Merge(new[] { resume, pause }, MusicSafetyState.Safe).TransportIntent
            .Should().Be(TransportIntentNeutral.PreferPause);
        MusicIntent.Merge(new[] { pause, silence, resume }, MusicSafetyState.Safe).TransportIntent
            .Should().Be(TransportIntentNeutral.PreferSilence);
    }

    [Fact]
    public void Merge_FloorMaxWins()
    {
        var a = MusicIntent.Create(floorVolumePercent: 30);
        var b = MusicIntent.Create(floorVolumePercent: 50);
        var c = MusicIntent.Create(floorVolumePercent: 40);

        MusicIntent.Merge(new[] { a, b, c }, MusicSafetyState.Safe).FloorVolumePercent.Should().Be(50);
    }

    [Fact]
    public void Merge_CeilingMinWins()
    {
        var a = MusicIntent.Create(ceilingVolumePercent: 90);
        var b = MusicIntent.Create(ceilingVolumePercent: 60);
        var c = MusicIntent.Create(ceilingVolumePercent: 75);

        MusicIntent.Merge(new[] { a, b, c }, MusicSafetyState.Safe).CeilingVolumePercent.Should().Be(60);
    }

    [Fact]
    public void Merge_FloorClampsToCeilingWhenConflicts()
    {
        var floor = MusicIntent.Create(floorVolumePercent: 80);
        var ceiling = MusicIntent.Create(ceilingVolumePercent: 50);

        var merged = MusicIntent.Merge(new[] { floor, ceiling }, MusicSafetyState.Safe);

        merged.FloorVolumePercent.Should().Be(50);
        merged.CeilingVolumePercent.Should().Be(50);
    }

    [Fact]
    public void Merge_OmitsFloorOrCeilingWhenNoIntentSpecifiesIt()
    {
        var only = MusicIntent.Create(transportIntent: TransportIntentNeutral.PreferResume);

        var merged = MusicIntent.Merge(new[] { only }, MusicSafetyState.Safe);

        merged.FloorVolumePercent.Should().BeNull();
        merged.CeilingVolumePercent.Should().BeNull();
    }

    [Fact]
    public void Merge_GainBiasSumsAndClampsToUnitRange()
    {
        var a = MusicIntent.Create(gainBias: 0.7f);
        var b = MusicIntent.Create(gainBias: 0.6f);

        MusicIntent.Merge(new[] { a, b }, MusicSafetyState.Safe).GainBias.Should().Be(1f);
    }

    [Fact]
    public void Merge_GainBiasClampsNegative()
    {
        var a = MusicIntent.Create(gainBias: -0.8f);
        var b = MusicIntent.Create(gainBias: -0.8f);

        MusicIntent.Merge(new[] { a, b }, MusicSafetyState.Safe).GainBias.Should().Be(-1f);
    }

    [Fact]
    public void Merge_CooldownTakesMax()
    {
        var a = MusicIntent.Create(cooldownHint: TimeSpan.FromSeconds(1));
        var b = MusicIntent.Create(cooldownHint: TimeSpan.FromSeconds(3));
        var c = MusicIntent.Create(cooldownHint: TimeSpan.FromSeconds(2));

        MusicIntent.Merge(new[] { a, b, c }, MusicSafetyState.Safe).CooldownHint.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Merge_ReasonsJoinedAndDeduplicated()
    {
        var a = MusicIntent.Create(reason: "scenario:duck");
        var b = MusicIntent.Create(reason: "scenario:duck");
        var c = MusicIntent.Create(reason: "scenario:cooldown");
        var d = MusicIntent.Create(reason: "");

        var merged = MusicIntent.Merge(new[] { a, b, c, d }, MusicSafetyState.Safe);

        merged.Reason.Should().Be("scenario:duck;scenario:cooldown");
    }

    [Fact]
    public void Merge_SafetyOverrideAllowedIsAndAcrossIntents()
    {
        var permissive = MusicIntent.Create(safetyOverrideAllowed: true);
        var strict = MusicIntent.Create(safetyOverrideAllowed: false);

        MusicIntent.Merge(new[] { permissive, strict }, MusicSafetyState.Safe)
            .SafetyOverrideAllowed.Should().BeFalse();
        MusicIntent.Merge(new[] { permissive, permissive }, MusicSafetyState.Safe)
            .SafetyOverrideAllowed.Should().BeTrue();
    }
}
