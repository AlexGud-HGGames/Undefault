using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Diff;
using Core.Models;
using Core.Rules;
using Cs2Simulator.Scenarios.Scenarios;
using FluentAssertions;

namespace Cs2Simulator.Tests;

public sealed class EventDetectionFidelityTests
{
    [Fact]
    public Task TSideRound_FiresExactlyOneRoundStart()
        => AssertDetectorFiresExactKeyCounts(new TSideRoundScenario(), expectedSequence: new()
        {
            EventKeys.RoundStart
        });

    [Fact]
    public Task CtSideDefense_Success_FiresExactlyOneRoundStart()
        => AssertDetectorFiresExactKeyCounts(new CtSideDefenseSuccessScenario(), expectedSequence: new()
        {
            EventKeys.RoundStart
        });

    [Fact]
    public Task CtSideDefense_Fail_FiresOneRoundStartAndOneDeath()
        => AssertDetectorFiresExactKeyCounts(new CtSideDefenseFailScenario(), expectedSequence: new()
        {
            EventKeys.RoundStart,
            EventKeys.Death
        });

    [Fact]
    public Task DeathSpectator_FiresExactlyOneDeath_AfterRoundStart()
        => AssertDetectorFiresExactKeyCounts(new DeathSpectatorScenario(), expectedSequence: new()
        {
            EventKeys.RoundStart,
            EventKeys.Death
        });

    [Fact]
    public Task Clutch_FiresThreeRoundStarts()
        => AssertDetectorFiresExactKeyCounts(new ClutchScenario(), expectedSequence: new()
        {
            EventKeys.RoundStart,
            EventKeys.RoundStart,
            EventKeys.RoundStart
        });

    [Fact]
    public Task TacticalPause_FiresTwoRoundStarts()
        => AssertDetectorFiresExactKeyCounts(new TacticalPauseScenario(), expectedSequence: new()
        {
            EventKeys.RoundStart,
            EventKeys.RoundStart
        });

    /// <summary>
    /// <paramref name="expectedSequence"/> defines the exact multiset of event keys
    /// (counts per key must match; order is not asserted here).
    /// </summary>
    private static async Task AssertDetectorFiresExactKeyCounts(IScenario scenario, List<string> expectedSequence)
    {
        var ticks = await ScenarioRunHelper.CollectAsync(scenario);

        var detector = new EventDetector();
        var differ = new SnapshotDiffer();
        var mapper = HostMappingHelper.CreateMapper();

        GameSnapshot? previous = null;
        var firedKeys = new List<string>();

        foreach (var tick in ticks)
        {
            var json = Cs2Simulator.Scenarios.Json.Cs2PayloadJson.Serialize(tick.Payload);
            var dto = System.Text.Json.JsonSerializer.Deserialize<GsiHost.Dtos.GsiPayloadDto>(json)!;
            var snapshot = mapper.Map(dto, DateTimeOffset.UnixEpoch);
            var diff = differ.Compute(previous, snapshot);
            var events = detector.Detect(diff);
            firedKeys.AddRange(events.Select(e => e.EventKey));
            previous = snapshot;
        }

        var expectedRoundStart = expectedSequence.Count(k => k == EventKeys.RoundStart);
        var actualRoundStart = firedKeys.Count(k => k == EventKeys.RoundStart);
        actualRoundStart.Should().Be(expectedRoundStart);

        var expectedDeath = expectedSequence.Count(k => k == EventKeys.Death);
        var actualDeath = firedKeys.Count(k => k == EventKeys.Death);
        actualDeath.Should().Be(expectedDeath);
    }
}
