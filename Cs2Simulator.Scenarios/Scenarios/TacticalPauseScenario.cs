using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Cs2Simulator.Scenarios.State;
using Cs2Simulator.Scenarios.Ticks;

namespace Cs2Simulator.Scenarios.Scenarios;

/// <summary>
/// Mid-match tactical pause. Sequence after the first live round:
/// <c>map.phase=intermission</c> for a long stretch, then we cycle
/// <c>intermission -&gt; warmup -&gt; live</c> on resume so the
/// warmup-to-live transition re-fires <c>round_start</c> in the host.
/// </summary>
public sealed class TacticalPauseScenario : ScenarioBase, IScenario
{
    public string Id => "tactical-pause";
    public string Name => "Tactical pause and resume";
    public string Description =>
        "Freezetime -> live -> tactical pause (intermission) -> resume via warmup -> round_start fires again.";

    public async IAsyncEnumerable<SimulatedTick> Run(
        SimulationState state,
        [EnumeratorCancellation] CancellationToken ct)
    {
        state.PlayerTeam = "CT";
        state.MapName = "de_dust2";

        await Task.Yield();

        ct.ThrowIfCancellationRequested();
        yield return Seed(state, TimeSpan.Zero);

        ct.ThrowIfCancellationRequested();
        yield return GoLive(state, TimeSpan.FromSeconds(2));

        ct.ThrowIfCancellationRequested();
        yield return Freezetime(state, TimeSpan.FromSeconds(3), "freezetime");

        ct.ThrowIfCancellationRequested();
        yield return RoundLive(state, TimeSpan.FromSeconds(2), "round live");

        state.MapPhase = "intermission";
        state.RoundPhase = "freezetime";
        ct.ThrowIfCancellationRequested();
        yield return Custom(state, TimeSpan.FromSeconds(2), "tactical pause requested");

        ct.ThrowIfCancellationRequested();
        yield return Custom(state, TimeSpan.FromSeconds(10), "tactical pause: still paused");

        ct.ThrowIfCancellationRequested();
        yield return Custom(state, TimeSpan.FromSeconds(10), "tactical pause: still paused");

        state.MapPhase = "warmup";
        ct.ThrowIfCancellationRequested();
        yield return Custom(state, TimeSpan.FromSeconds(2), "resume sequence: warmup");

        state.MapPhase = "live";
        state.RoundPhase = "freezetime";
        ct.ThrowIfCancellationRequested();
        yield return Custom(state, TimeSpan.FromSeconds(2), "resume: warmup->live", ScenarioEventKeys.RoundStart);

        ct.ThrowIfCancellationRequested();
        yield return RoundLive(state, TimeSpan.FromSeconds(2), "round live (resumed)");

        ct.ThrowIfCancellationRequested();
        yield return RoundOver(state, TimeSpan.FromSeconds(3), "CT", "round over after resume");
    }
}
