namespace GsiHost.Configuration;

public sealed class TimelineOptions
{
    public const string SectionName = "Timeline";

    public bool Enabled { get; set; } = true;

    public int MaxInMemoryEntries { get; set; } = 1_000;

    public string Directory { get; set; } = "timeline";

    public int EpisodeBeforeEntryCount { get; set; } = 10;

    public int EpisodeAfterEntryCount { get; set; } = 5;
}
