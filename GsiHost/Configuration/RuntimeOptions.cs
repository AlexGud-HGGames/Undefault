using Microsoft.Extensions.Configuration;

namespace GsiHost.Configuration;

public sealed class RuntimeOptions
{
    public const string SectionName = "Runtime";

    public string Mode { get; set; } = RuntimeModes.ScenarioPlayback;

    public string NormalizedMode => RuntimeModes.Normalize(Mode);

    public bool IsIntentCapture => RuntimeModes.IsIntentCapture(Mode);

    public static RuntimeOptions From(IConfiguration configuration)
    {
        var mode = configuration.GetSection(SectionName)["Mode"];
        return new RuntimeOptions { Mode = RuntimeModes.Normalize(mode) };
    }
}

public static class RuntimeModes
{
    public const string ScenarioPlayback = "scenario_playback";
    public const string IntentCapture = "intent_capture";

    public static string Normalize(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return ScenarioPlayback;
        }

        var normalized = mode.Trim().Replace('-', '_').ToLowerInvariant();
        return normalized switch
        {
            "scenario" or "scenario_playback" or "scenarioplayback" => ScenarioPlayback,
            "intent" or "intent_capture" or "intentcapture" => IntentCapture,
            _ => ScenarioPlayback
        };
    }

    public static bool IsIntentCapture(string? mode)
    {
        return string.Equals(Normalize(mode), IntentCapture, StringComparison.OrdinalIgnoreCase);
    }
}
