namespace GsiHost.Configuration;

public sealed class ManualMusicActionOptions
{
    public const string SectionName = "ManualMusicActions";

    public bool Enabled { get; set; } = true;

    public List<string> AllowedEventKeys { get; set; } = new();
}
