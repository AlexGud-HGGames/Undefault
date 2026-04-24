using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Cs2Simulator.Scenarios.Scenarios;
using Cs2Simulator.Scenarios.State;
using Cs2Simulator.Scenarios.Ticks;

namespace Cs2Simulator.Tests.Fakes;

/// <summary>
/// Two-tick scenario used by ScenarioRunnerTests. Not picked up by
/// catalog discovery against the Scenarios assembly (it lives here in
/// the test project), so it can't pollute the catalog tests.
/// </summary>
internal sealed class TwoTickScenario : ScenarioBase, IScenario
{
    public string Id => "test-two-tick";
    public string Name => "Two ticks for runner tests";
    public string Description => "Seed + one live tick.";

    public TimeSpan SeedDelay { get; init; } = TimeSpan.Zero;
    public TimeSpan LiveDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    public async IAsyncEnumerable<SimulatedTick> Run(
        SimulationState state,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        yield return Seed(state, SeedDelay);

        ct.ThrowIfCancellationRequested();
        yield return GoLive(state, LiveDelay);
    }
}

internal sealed class HangingScenario : ScenarioBase, IScenario
{
    public string Id => "test-hanging";
    public string Name => "Hangs forever";
    public string Description => "Yields a seed then never completes.";

    public async IAsyncEnumerable<SimulatedTick> Run(
        SimulationState state,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();
        yield return Seed(state, TimeSpan.Zero);

        while (!ct.IsCancellationRequested)
        {
            yield return Custom(state, TimeSpan.FromMilliseconds(50), "still running");
        }
    }
}
