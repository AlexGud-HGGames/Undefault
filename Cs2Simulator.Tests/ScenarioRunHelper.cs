using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cs2Simulator.Scenarios.Scenarios;
using Cs2Simulator.Scenarios.State;
using Cs2Simulator.Scenarios.Ticks;

namespace Cs2Simulator.Tests;

internal static class ScenarioRunHelper
{
    public static async Task<List<SimulatedTick>> CollectAsync(
        IScenario scenario,
        SimulationState? state = null)
    {
        state ??= new SimulationState();
        var ticks = new List<SimulatedTick>();
        await foreach (var tick in scenario.Run(state, CancellationToken.None))
        {
            ticks.Add(tick);
        }
        return ticks;
    }
}
