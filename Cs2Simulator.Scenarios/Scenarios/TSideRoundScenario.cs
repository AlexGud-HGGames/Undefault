using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Cs2Simulator.Scenarios.State;
using Cs2Simulator.Scenarios.Ticks;

namespace Cs2Simulator.Scenarios.Scenarios;

public sealed class TSideRoundScenario : ScenarioBase, IScenario
{
    public string Id => "t-side-round";
    public string Name => "T-side round (plant)";
    public string Description =>
        "Freezetime -> live -> push to bombsite -> plant -> post-plant rotations -> T win.";

    public async IAsyncEnumerable<SimulatedTick> Run(
        SimulationState state,
        [EnumeratorCancellation] CancellationToken ct)
    {
        state.PlayerTeam = "T";
        state.MapName = "de_mirage";
        state.PlayerWeapons["weapon_1"] = state.PlayerWeapons["weapon_1"] with
        {
            Name = "weapon_ak47",
            Type = "Rifle",
            AmmoClip = 30,
            AmmoClipMax = 30,
            AmmoReserve = 90,
            State = "active"
        };

        await Task.Yield();

        ct.ThrowIfCancellationRequested();
        yield return Seed(state, TimeSpan.Zero);

        ct.ThrowIfCancellationRequested();
        yield return GoLive(state, TimeSpan.FromSeconds(2));

        ct.ThrowIfCancellationRequested();
        yield return Freezetime(state, TimeSpan.FromSeconds(3), "freezetime: buy phase");

        ct.ThrowIfCancellationRequested();
        yield return RoundLive(state, TimeSpan.FromSeconds(3), "round live: leaving spawn");

        ct.ThrowIfCancellationRequested();
        yield return Move(state, TimeSpan.FromSeconds(2), "1200.0, -300.0, 64.0", "rotating to A apartments");

        ct.ThrowIfCancellationRequested();
        yield return Move(state, TimeSpan.FromSeconds(3), "1500.0, 100.0, 64.0", "entering A site");

        state.PlayerRoundKills = 1;
        state.PlayerRoundTotalDamage = 100;
        state.PlayerKills += 1;
        ct.ThrowIfCancellationRequested();
        yield return Custom(state, TimeSpan.FromSeconds(2), "trade kill on CT defender");

        ct.ThrowIfCancellationRequested();
        yield return BombPlant(state, TimeSpan.FromSeconds(2));

        ct.ThrowIfCancellationRequested();
        yield return Move(state, TimeSpan.FromSeconds(3), "1520.0, 120.0, 64.0", "post-plant: holding angles");

        ct.ThrowIfCancellationRequested();
        yield return Move(state, TimeSpan.FromSeconds(3), "1540.0, 130.0, 64.0", "post-plant: rotating cover");

        state.PlayerMvps += 1;
        state.PlayerScore += 2;
        ct.ThrowIfCancellationRequested();
        yield return RoundOver(state, TimeSpan.FromSeconds(2), "T", "round over: T wins on bomb timer");
    }
}
