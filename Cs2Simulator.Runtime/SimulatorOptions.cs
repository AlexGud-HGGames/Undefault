namespace Cs2Simulator.Runtime;

public sealed class SimulatorOptions
{
    public string Endpoint { get; set; } = "http://127.0.0.1:5292";
    public string DefaultSpeed { get; set; } = "1";
    public bool ResetBeforeRun { get; set; } = true;

    /// <summary>
    /// When false, the runner emits tick-level logs at Debug severity.
    /// </summary>
    public bool LogVerbose { get; set; } = true;
}
