using System;
using Cs2Simulator.Scenarios.Models;

namespace Cs2Simulator.Scenarios.Ticks;

/// <summary>
/// One emit step in a scenario: how long to wait before sending,
/// the payload to send, a human-readable description, and an optional
/// hint about which normalized event the host's <c>EventDetector</c>
/// is expected to fire on this snapshot.
/// </summary>
/// <param name="Delay">
/// Nominal pre-send delay. Also used to advance the simulated clock
/// (see <see cref="State.SimulatedClock"/>). The runner's speed
/// multiplier scales the wall-clock wait, never this nominal value.
/// </param>
/// <param name="Payload">The CS2-shaped payload to POST to <c>/gsi</c>.</param>
/// <param name="Description">Short human label, e.g. "freezetime -&gt; live".</param>
/// <param name="ExpectedEventKey">
/// Limited to <c>Core.Models.EventKeys.RoundStart</c>,
/// <c>Core.Models.EventKeys.Death</c>, or <c>null</c>. The current host
/// detector cannot produce other keys from these payloads. Used only for
/// runner logging and offline fidelity assertions.
/// </param>
public sealed record SimulatedTick(
    TimeSpan Delay,
    Cs2Payload Payload,
    string Description,
    string? ExpectedEventKey = null);
