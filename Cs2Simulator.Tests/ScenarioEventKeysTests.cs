using Core.Models;
using Cs2Simulator.Scenarios.Scenarios;
using FluentAssertions;

namespace Cs2Simulator.Tests;

public sealed class ScenarioEventKeysTests
{
    [Fact]
    public void RoundStartKey_MatchesCoreEventKeys()
    {
        ScenarioEventKeys.RoundStart.Should().Be(EventKeys.RoundStart);
    }

    [Fact]
    public void DeathKey_MatchesCoreEventKeys()
    {
        ScenarioEventKeys.Death.Should().Be(EventKeys.Death);
    }
}
