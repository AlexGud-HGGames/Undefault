using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Cs2Simulator.Scenarios.State;
using Cs2Simulator.Scenarios.Ticks;

namespace Cs2Simulator.Scenarios.Scenarios;

/// <summary>
/// Player dies, then payloads switch to the spectated teammate.
/// Note: SnapshotDiffer is identity-blind, so the "respawn-looking"
/// spectator ticks do not re-fire <c>death</c> and do not re-fire
/// <c>round_start</c> as long as <c>map.round</c> is unchanged.
/// </summary>
public sealed class DeathSpectatorScenario : ScenarioBase, IScenario
{
    public string Id => "death-spectator";
    public string Name => "Death and spectate";
    public string Description =>
        "Freezetime -> live -> player dies -> follow-up payloads from spectated teammate -> round continues.";

    public async IAsyncEnumerable<SimulatedTick> Run(
        SimulationState state,
        [EnumeratorCancellation] CancellationToken ct)
    {
        state.PlayerTeam = "T";
        state.MapName = "de_anubis";

        await Task.Yield();

        ct.ThrowIfCancellationRequested();
        yield return Seed(state, TimeSpan.Zero);

        ct.ThrowIfCancellationRequested();
        yield return GoLive(state, TimeSpan.FromSeconds(2));

        ct.ThrowIfCancellationRequested();
        yield return RoundLive(state, TimeSpan.FromSeconds(3), "round live: pushing");

        state.PlayerHealth = 45;
        ct.ThrowIfCancellationRequested();
        yield return Move(state, TimeSpan.FromSeconds(2), "200.0, 100.0, 64.0", "took damage in duel");

        ct.ThrowIfCancellationRequested();
        yield return Die(state, TimeSpan.FromSeconds(1), "player killed");

        state.PlayerSteamId = "76561198000000002";
        state.PlayerName = "TeammateAlpha";
        state.PlayerObserverSlot = 2;
        state.PlayerHealth = 100;
        state.PlayerArmor = 100;
        state.PlayerActivity = "playing";
        ct.ThrowIfCancellationRequested();
        yield return Move(state, TimeSpan.FromSeconds(2), "300.0, 200.0, 64.0", "spectating teammate alpha");

        state.PlayerSteamId = "76561198000000003";
        state.PlayerName = "TeammateBravo";
        state.PlayerObserverSlot = 3;
        ct.ThrowIfCancellationRequested();
        yield return Move(state, TimeSpan.FromSeconds(3), "400.0, 250.0, 64.0", "spectating teammate bravo");

        ct.ThrowIfCancellationRequested();
        yield return RoundOver(state, TimeSpan.FromSeconds(2), "T", "round over: teammates won");
    }
}
