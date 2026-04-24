using System;
using Cs2Simulator.Scenarios.State;
using Cs2Simulator.Scenarios.Ticks;

namespace Cs2Simulator.Scenarios.Scenarios;

/// <summary>
/// Authoring helpers shared by all built-in scenarios. Each helper:
///   1. mutates <see cref="SimulationState"/> for the next tick,
///   2. advances <see cref="SimulationState.Clock"/> by the nominal delay,
///   3. builds a <see cref="SimulatedTick"/> from the resulting state.
/// The runner only uses <c>Delay</c> for wall-clock waits; the simulated
/// clock baked into <c>provider.timestamp</c> is independent of speed.
/// </summary>
public abstract class ScenarioBase
{
    /// <summary>
    /// MANDATORY first tick. Holds the world in pre-match warmup so the
    /// next tick is the first one EventDetector will actually evaluate
    /// against (the very first snapshot is short-circuited by
    /// <c>EventDetector.Detect</c> by design).
    /// </summary>
    protected static SimulatedTick Seed(SimulationState state, TimeSpan delay)
    {
        state.MapPhase = "warmup";
        state.MapRound = 0;
        state.RoundPhase = "freezetime";
        state.BombState = null;
        state.WinTeam = null;
        state.PlayerHealth = 100;
        state.PlayerActivity = "playing";
        return Emit(state, delay, "seed: warmup, round 0");
    }

    /// <summary>
    /// First round of the match: drives <c>round_start</c> via the
    /// <c>map.phase</c> warmup-to-live transition.
    /// </summary>
    protected static SimulatedTick GoLive(SimulationState state, TimeSpan delay)
    {
        state.MapPhase = "live";
        state.MapRound = 1;
        state.RoundPhase = "freezetime";
        state.BombState = null;
        state.WinTeam = null;
        return Emit(state, delay, "go live: map.phase warmup->live, round 1", ScenarioEventKeys.RoundStart);
    }

    /// <summary>
    /// Subsequent round: drives <c>round_start</c> via the
    /// <c>map.round</c> increment.
    /// </summary>
    protected static SimulatedTick NextRound(SimulationState state, TimeSpan delay)
    {
        state.MapRound += 1;
        state.MapPhase = "live";
        state.RoundPhase = "freezetime";
        state.BombState = null;
        state.WinTeam = null;
        state.PlayerRoundKills = 0;
        state.PlayerRoundKillHeadshots = 0;
        state.PlayerRoundTotalDamage = 0;
        state.PlayerHealth = 100;
        state.PlayerSteamId = SimulationState.DefaultPlayerSteamId;
        state.PlayerName = SimulationState.DefaultPlayerName;
        state.PlayerObserverSlot = SimulationState.DefaultPlayerObserverSlot;
        return Emit(state, delay, $"next round: map.round={state.MapRound}", ScenarioEventKeys.RoundStart);
    }

    protected static SimulatedTick Freezetime(SimulationState state, TimeSpan delay, string? note = null)
    {
        state.RoundPhase = "freezetime";
        return Emit(state, delay, note ?? "freezetime");
    }

    protected static SimulatedTick RoundLive(SimulationState state, TimeSpan delay, string? note = null)
    {
        state.RoundPhase = "live";
        return Emit(state, delay, note ?? "round live");
    }

    protected static SimulatedTick Move(
        SimulationState state,
        TimeSpan delay,
        string position,
        string? note = null)
    {
        state.PlayerPosition = position;
        return Emit(state, delay, note ?? $"move to {position}");
    }

    protected static SimulatedTick BombPlant(SimulationState state, TimeSpan delay)
    {
        state.RoundPhase = "live";
        state.BombState = "planted";
        return Emit(state, delay, "bomb planted");
    }

    protected static SimulatedTick BombDefuse(SimulationState state, TimeSpan delay)
    {
        state.RoundPhase = "live";
        state.BombState = "defused";
        return Emit(state, delay, "bomb defused");
    }

    protected static SimulatedTick BombExplode(SimulationState state, TimeSpan delay)
    {
        state.RoundPhase = "over";
        state.BombState = "exploded";
        return Emit(state, delay, "bomb exploded");
    }

    protected static SimulatedTick Die(SimulationState state, TimeSpan delay, string? note = null)
    {
        state.PlayerHealth = 0;
        state.PlayerArmor = 0;
        state.PlayerDeaths += 1;
        return Emit(state, delay, note ?? "player died", ScenarioEventKeys.Death);
    }

    protected static SimulatedTick RoundOver(
        SimulationState state,
        TimeSpan delay,
        string winTeam,
        string? note = null)
    {
        state.RoundPhase = "over";
        state.WinTeam = winTeam;
        if (string.Equals(winTeam, "T", StringComparison.OrdinalIgnoreCase))
        {
            state.TeamT = state.TeamT with { Score = (state.TeamT.Score ?? 0) + 1 };
        }
        else if (string.Equals(winTeam, "CT", StringComparison.OrdinalIgnoreCase))
        {
            state.TeamCt = state.TeamCt with { Score = (state.TeamCt.Score ?? 0) + 1 };
        }

        return Emit(state, delay, note ?? $"round over: {winTeam} win");
    }

    protected static SimulatedTick Custom(
        SimulationState state,
        TimeSpan delay,
        string description,
        string? expectedEventKey = null)
    {
        return Emit(state, delay, description, expectedEventKey);
    }

    private static SimulatedTick Emit(
        SimulationState state,
        TimeSpan delay,
        string description,
        string? expectedEventKey = null)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), "Tick delays cannot be negative.");
        }

        state.Clock.Advance(delay);
        var payload = Cs2PayloadBuilder.Build(state);
        return new SimulatedTick(delay, payload, description, expectedEventKey);
    }
}
