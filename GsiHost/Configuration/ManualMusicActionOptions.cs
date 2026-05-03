namespace GsiHost.Configuration;

public sealed class ManualMusicActionOptions
{
    public const string SectionName = "ManualMusicActions";

    public bool? Enabled { get; set; }

    public List<string> AllowedEventKeys { get; set; } = new();

    public List<ManualMusicActionMappingOptions> CommandMappings { get; set; } = new();

    public bool IsEnabled(RuntimeOptions runtime)
    {
        return Enabled ?? runtime.IsIntentCapture;
    }

    public IReadOnlyList<ManualMusicActionMappingOptions> GetCommandMappings()
    {
        return CommandMappings.Count == 0
            ? DefaultCommandMappings
            : CommandMappings;
    }

    private static readonly ManualMusicActionMappingOptions[] DefaultCommandMappings =
    {
        new("custom:music_mute", "duck", 0),
        new("custom:music_pause", "pause"),
        new("custom:music_resume", "resume"),
        new("custom:music_restore", "restore_volume")
    };
}

public sealed record ManualMusicActionMappingOptions(
    string EventKey,
    string Command,
    int? VolumePercent = null);
