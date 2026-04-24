using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Cs2Simulator.Scenarios.Discovery;
using Cs2Simulator.Scenarios.Scenarios;
using Cs2Simulator.Scenarios.State;
using Cs2Simulator.Scenarios.Ticks;
using FluentAssertions;

namespace Cs2Simulator.Tests;

public sealed class ScenarioCatalogTests
{
    [Fact]
    public void Discover_FindsAllBuiltInScenarios()
    {
        var catalog = ScenarioCatalog.Discover(typeof(IScenario).Assembly);

        catalog.All.Select(s => s.Id).Should().BeEquivalentTo(new[]
        {
            "ct-defense",
            "ct-defense-fail",
            "clutch-1v3",
            "death-spectator",
            "t-side-round",
            "tactical-pause"
        });
    }

    [Fact]
    public void Discover_AllScenariosHaveNonEmptyMetadata()
    {
        var catalog = ScenarioCatalog.Discover(typeof(IScenario).Assembly);

        foreach (var scenario in catalog.All)
        {
            scenario.Id.Should().NotBeNullOrWhiteSpace();
            scenario.Name.Should().NotBeNullOrWhiteSpace();
            scenario.Description.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Discover_TryGet_IsCaseInsensitive()
    {
        var catalog = ScenarioCatalog.Discover(typeof(IScenario).Assembly);

        catalog.TryGet("T-SIDE-ROUND", out var scenario).Should().BeTrue();
        scenario!.Id.Should().Be("t-side-round");
    }

    [Fact]
    public void Discover_ThrowsWhenScenarioMissingParameterlessConstructor()
    {
        var asm = typeof(BadScenarioFixtureLoader).Assembly;

        Action act = () => ScenarioCatalog.Discover(asm);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*public parameterless constructor*BadScenarioFixture*");
    }

    private static class BadScenarioFixtureLoader { }
}

/// <summary>
/// Must stay without a public parameterless ctor: <see cref="ScenarioCatalog.Discover"/>
/// fails on missing ctor before it tries to activate types, so test fakes such as
/// <c>TwoTickScenario</c> in this assembly are never instantiated by the catalog.
/// </summary>
internal sealed class BadScenarioFixture : IScenario
{
    public BadScenarioFixture(string requiredArg) { _ = requiredArg; }

    public string Id => "bad";
    public string Name => "bad";
    public string Description => "no public parameterless ctor";

#pragma warning disable CS1998
    public async IAsyncEnumerable<SimulatedTick> Run(
        SimulationState state,
        [EnumeratorCancellation] CancellationToken ct)
    {
        yield break;
    }
#pragma warning restore CS1998
}
