namespace GsiHost.Configuration;

public sealed class ManualMusicActionOptions
{
    public const string SectionName = "ManualMusicActions";

    public bool Enabled { get; set; }

    public List<string> AllowedEventKeys { get; set; } = new();

    public bool IsEnabled(RuntimeOptions runtime) => Enabled && runtime.IsIntentCapture;
}
