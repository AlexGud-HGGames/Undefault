namespace Cs2Simulator.Runtime;

public sealed class ScenarioRunOptions
{
    public Speed Speed { get; set; } = Speed.Normal;
    public bool StepMode { get; set; }
    public bool ResetBeforeRun { get; set; } = true;

    /// <summary>
    /// When false, per-tick detail is logged at Debug instead of Information.
    /// </summary>
    public bool VerboseLogging { get; set; } = true;
}
