using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Cs2Simulator.Scenarios.State;
using Cs2Simulator.Scenarios.Ticks;

namespace Cs2Simulator.Scenarios.Scenarios;

public abstract class CtSideDefenseScenarioBase : ScenarioBase
{
    protected static async IAsyncEnumerable<SimulatedTick> RunCore(
        SimulationState state,
        bool defuseSucceeds,
        [EnumeratorCancellation] CancellationToken ct)
    {
        state.PlayerTeam = "CT";
        state.MapName = "de_inferno";
        state.PlayerArmor = 100;
        state.PlayerHelmet = true;
        state.PlayerDefuseKit = true;
        state.PlayerWeapons["weapon_1"] = state.PlayerWeapons["weapon_1"] with
        {
            Name = "weapon_m4a1_silencer",
            Type = "Rifle",
            AmmoClip = 20,
            AmmoClipMax = 20,
            AmmoReserve = 80,
            State = "active"
        };

        await Task.Yield();

        ct.ThrowIfCancellationRequested();
        yield return Seed(state, TimeSpan.Zero);

        ct.ThrowIfCancellationRequested();
        yield return GoLive(state, TimeSpan.FromSeconds(2));

        ct.ThrowIfCancellationRequested();
        yield return Freezetime(state, TimeSpan.FromSeconds(3), "freezetime: defensive setup");

        ct.ThrowIfCancellationRequested();
        yield return RoundLive(state, TimeSpan.FromSeconds(3), "round live: holding A site");

        state.PlayerHealth = 80;
        ct.ThrowIfCancellationRequested();
        yield return Move(state, TimeSpan.FromSeconds(2), "2050.0, 580.0, 96.0", "enemy contact: traded shots");

        ct.ThrowIfCancellationRequested();
        yield return BombPlant(state, TimeSpan.FromSeconds(3));

        ct.ThrowIfCancellationRequested();
        yield return Move(state, TimeSpan.FromSeconds(3), "2060.0, 600.0, 96.0", "retake: regrouping at apps");

        ct.ThrowIfCancellationRequested();
        yield return Move(state, TimeSpan.FromSeconds(2), "2080.0, 620.0, 96.0", "retake: pushing site");

        if (defuseSucceeds)
        {
            ct.ThrowIfCancellationRequested();
            yield return BombDefuse(state, TimeSpan.FromSeconds(5));

            state.PlayerScore += 2;
            ct.ThrowIfCancellationRequested();
            yield return RoundOver(state, TimeSpan.FromSeconds(1), "CT", "round over: CT defuse");
        }
        else
        {
            state.PlayerHealth = 0;
            state.PlayerDeaths += 1;
            ct.ThrowIfCancellationRequested();
            yield return Custom(state, TimeSpan.FromSeconds(3), "retake failed: player killed", ScenarioEventKeys.Death);

            ct.ThrowIfCancellationRequested();
            yield return BombExplode(state, TimeSpan.FromSeconds(3));

            ct.ThrowIfCancellationRequested();
            yield return RoundOver(state, TimeSpan.FromSeconds(1), "T", "round over: bomb exploded, T win");
        }
    }
}

public sealed class CtSideDefenseSuccessScenario : CtSideDefenseScenarioBase, IScenario
{
    public string Id => "ct-defense";
    public string Name => "CT-side defense (defuse)";
    public string Description =>
        "Freezetime -> live -> enemy contact -> bomb planted -> retake -> defuse -> CT win.";

    public IAsyncEnumerable<SimulatedTick> Run(SimulationState state, CancellationToken ct)
    {
        return RunCore(state, defuseSucceeds: true, ct);
    }
}

public sealed class CtSideDefenseFailScenario : CtSideDefenseScenarioBase, IScenario
{
    public string Id => "ct-defense-fail";
    public string Name => "CT-side defense (bomb explodes)";
    public string Description =>
        "Same as ct-defense but the retake fails: player dies and the bomb explodes.";

    public IAsyncEnumerable<SimulatedTick> Run(SimulationState state, CancellationToken ct)
    {
        return RunCore(state, defuseSucceeds: false, ct);
    }
}
