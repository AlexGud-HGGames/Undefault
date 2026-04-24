namespace Cs2Simulator.Scenarios.Scenarios;

/// <summary>
/// Mirror of the host's <c>Core.Models.EventKeys</c> values that scenarios
/// can legitimately predict. Kept as plain strings so the Scenarios library
/// has no dependency on Core. A test in <c>Cs2Simulator.Tests</c> asserts
/// these stay in sync with Core.
/// </summary>
public static class ScenarioEventKeys
{
    public const string RoundStart = "round_start";
    public const string Death = "death";
}
