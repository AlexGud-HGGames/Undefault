using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Cs2Simulator.Scenarios.State;
using Cs2Simulator.Scenarios.Ticks;

namespace Cs2Simulator.Scenarios.Scenarios;

public sealed class ClutchScenario : ScenarioBase, IScenario
{
    public string Id => "clutch-1v3";
    public string Name => "Clutch 1v3 over multiple rounds";
    public string Description =>
        "Several rounds (round_start fires per increment), then a tense round where match_stats and round_kills swing as the player wins a 1v3.";

    public async IAsyncEnumerable<SimulatedTick> Run(
        SimulationState state,
        [EnumeratorCancellation] CancellationToken ct)
    {
        state.PlayerTeam = "T";
        state.MapName = "de_nuke";
        state.PlayerArmor = 100;

        await Task.Yield();

        ct.ThrowIfCancellationRequested();
        yield return Seed(state, TimeSpan.Zero);

        ct.ThrowIfCancellationRequested();
        yield return GoLive(state, TimeSpan.FromSeconds(2));

        ct.ThrowIfCancellationRequested();
        yield return RoundLive(state, TimeSpan.FromSeconds(3), "round 1: live");

        state.PlayerKills += 1;
        ct.ThrowIfCancellationRequested();
        yield return Custom(state, TimeSpan.FromSeconds(3), "round 1: kill");

        ct.ThrowIfCancellationRequested();
        yield return RoundOver(state, TimeSpan.FromSeconds(1), "T", "round 1: T win");

        ct.ThrowIfCancellationRequested();
        yield return NextRound(state, TimeSpan.FromSeconds(2));

        ct.ThrowIfCancellationRequested();
        yield return RoundLive(state, TimeSpan.FromSeconds(3), "round 2: live");

        ct.ThrowIfCancellationRequested();
        yield return RoundOver(state, TimeSpan.FromSeconds(1), "CT", "round 2: CT win");

        ct.ThrowIfCancellationRequested();
        yield return NextRound(state, TimeSpan.FromSeconds(2));

        ct.ThrowIfCancellationRequested();
        yield return RoundLive(state, TimeSpan.FromSeconds(3), "round 3 (clutch): live");

        ct.ThrowIfCancellationRequested();
        yield return Custom(state, TimeSpan.FromSeconds(2), "clutch: 4 teammates down, 1v3");

        state.PlayerHealth = 35;
        ct.ThrowIfCancellationRequested();
        yield return Custom(state, TimeSpan.FromSeconds(2), "clutch: low HP, hunted by 3 CT");

        state.PlayerRoundKills = 1;
        state.PlayerKills += 1;
        ct.ThrowIfCancellationRequested();
        yield return Custom(state, TimeSpan.FromSeconds(2), "clutch: kill 1/3");

        state.PlayerRoundKills = 2;
        state.PlayerKills += 1;
        ct.ThrowIfCancellationRequested();
        yield return Custom(state, TimeSpan.FromSeconds(2), "clutch: kill 2/3");

        state.PlayerRoundKills = 3;
        state.PlayerKills += 1;
        state.PlayerRoundKillHeadshots = 1;
        ct.ThrowIfCancellationRequested();
        yield return Custom(state, TimeSpan.FromSeconds(2), "clutch: kill 3/3, ace");

        state.PlayerMvps += 1;
        state.PlayerScore += 4;
        ct.ThrowIfCancellationRequested();
        yield return RoundOver(state, TimeSpan.FromSeconds(1), "T", "clutch resolved: T win");
    }
}
