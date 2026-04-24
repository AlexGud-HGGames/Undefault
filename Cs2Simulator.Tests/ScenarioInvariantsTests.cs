using System.Linq;
using System.Threading.Tasks;
using Cs2Simulator.Scenarios.Scenarios;
using FluentAssertions;

namespace Cs2Simulator.Tests;

public sealed class ScenarioInvariantsTests
{
    [Fact]
    public async Task TSideRound_StartsWithSeed_AndReachesGoLive_AndPlantsBomb_AndOver()
    {
        var ticks = await ScenarioRunHelper.CollectAsync(new TSideRoundScenario());

        ticks.Should().NotBeEmpty();
        ticks[0].Payload.Map!.Phase.Should().Be("warmup");
        ticks[0].Payload.Map!.Round.Should().Be(0);

        ticks[1].Payload.Map!.Phase.Should().Be("live");
        ticks[1].Payload.Map!.Round.Should().Be(1);
        ticks[1].ExpectedEventKey.Should().Be(ScenarioEventKeys.RoundStart);

        ticks.Should().Contain(t => t.Payload.Round!.Bomb == "planted");
        var last = ticks.Last();
        last.Payload.Round!.Phase.Should().Be("over");
        last.Payload.Round!.WinTeam.Should().Be("T");
    }

    [Fact]
    public async Task CtSideDefense_Success_EndsWithDefuseAndCtWin()
    {
        var ticks = await ScenarioRunHelper.CollectAsync(new CtSideDefenseSuccessScenario());

        ticks[0].Payload.Map!.Phase.Should().Be("warmup");
        ticks[1].ExpectedEventKey.Should().Be(ScenarioEventKeys.RoundStart);
        ticks.Should().Contain(t => t.Payload.Round!.Bomb == "planted");
        ticks.Should().Contain(t => t.Payload.Round!.Bomb == "defused");
        var last = ticks.Last();
        last.Payload.Round!.WinTeam.Should().Be("CT");
    }

    [Fact]
    public async Task CtSideDefense_Fail_EndsWithBombExplosionAndTWin_AndPlayerDeath()
    {
        var ticks = await ScenarioRunHelper.CollectAsync(new CtSideDefenseFailScenario());

        ticks.Should().Contain(t => t.Payload.Round!.Bomb == "planted");
        ticks.Should().Contain(t => t.Payload.Round!.Bomb == "exploded");
        ticks.Where(t => t.Payload.Player!.State!.Health == 0).Should().NotBeEmpty();
        ticks.Where(t => t.ExpectedEventKey == ScenarioEventKeys.Death).Should().HaveCount(1);
        ticks.Last().Payload.Round!.WinTeam.Should().Be("T");
    }

    [Fact]
    public async Task Clutch_Has_MultipleRoundStartTransitions()
    {
        var ticks = await ScenarioRunHelper.CollectAsync(new ClutchScenario());

        ticks[0].Payload.Map!.Phase.Should().Be("warmup");
        ticks.Where(t => t.ExpectedEventKey == ScenarioEventKeys.RoundStart).Should().HaveCountGreaterThanOrEqualTo(3);
        ticks.Last().Payload.Round!.WinTeam.Should().Be("T");
    }

    [Fact]
    public async Task DeathSpectator_HasExactlyOneDeathTick_FollowedBySpectatorRespawn()
    {
        var ticks = await ScenarioRunHelper.CollectAsync(new DeathSpectatorScenario());

        var deathIndex = ticks.FindIndex(t => t.Payload.Player!.State!.Health == 0);
        deathIndex.Should().BeGreaterThan(0);
        ticks[deathIndex].ExpectedEventKey.Should().Be(ScenarioEventKeys.Death);

        ticks.Where(t => t.Payload.Player!.State!.Health == 0).Should().HaveCount(1);

        var afterDeath = ticks.Skip(deathIndex + 1).ToList();
        afterDeath.Should().NotBeEmpty();
        afterDeath.Should().Contain(t => t.Payload.Player!.State!.Health!.Value > 0);
    }

    [Fact]
    public async Task TacticalPause_CyclesIntermissionWarmupLive_AndExpectsSecondRoundStart()
    {
        var ticks = await ScenarioRunHelper.CollectAsync(new TacticalPauseScenario());

        var phases = ticks.Select(t => t.Payload.Map!.Phase).ToList();
        phases.Should().Contain("intermission");
        phases.Should().Contain("warmup");
        phases.Last().Should().Be("live");

        ticks.Where(t => t.ExpectedEventKey == ScenarioEventKeys.RoundStart).Should().HaveCount(2);
    }

    [Theory]
    [InlineData(typeof(TSideRoundScenario))]
    [InlineData(typeof(CtSideDefenseSuccessScenario))]
    [InlineData(typeof(CtSideDefenseFailScenario))]
    [InlineData(typeof(ClutchScenario))]
    [InlineData(typeof(DeathSpectatorScenario))]
    [InlineData(typeof(TacticalPauseScenario))]
    public async Task EveryScenario_StartsWithASeedTick(System.Type scenarioType)
    {
        var scenario = (IScenario)System.Activator.CreateInstance(scenarioType)!;
        var ticks = await ScenarioRunHelper.CollectAsync(scenario);

        ticks.Should().NotBeEmpty();
        ticks[0].Payload.Map!.Phase.Should().Be("warmup");
        ticks[0].Payload.Map!.Round.Should().Be(0);
        ticks[0].ExpectedEventKey.Should().BeNull();
    }

    [Theory]
    [InlineData(typeof(TSideRoundScenario))]
    [InlineData(typeof(CtSideDefenseSuccessScenario))]
    [InlineData(typeof(CtSideDefenseFailScenario))]
    [InlineData(typeof(ClutchScenario))]
    [InlineData(typeof(DeathSpectatorScenario))]
    [InlineData(typeof(TacticalPauseScenario))]
    public async Task EveryScenario_ProviderTimestamps_AreMonotonicallyNonDecreasing(System.Type scenarioType)
    {
        var scenario = (IScenario)System.Activator.CreateInstance(scenarioType)!;
        var ticks = await ScenarioRunHelper.CollectAsync(scenario);

        var timestamps = ticks.Select(t => t.Payload.Provider!.Timestamp!.Value).ToList();
        for (var i = 1; i < timestamps.Count; i++)
        {
            timestamps[i].Should().BeGreaterThanOrEqualTo(timestamps[i - 1]);
        }
    }
}
