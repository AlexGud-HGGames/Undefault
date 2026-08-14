namespace GsiHost.Configuration;

/// <summary>
/// Binds the <c>Music</c> section that selects the playback backend.
/// </summary>
public sealed class MusicProviderOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Music";

    /// <summary>
    /// Gets or sets the player backend name.
    /// </summary>
    /// <value>
    /// <c>Tauon</c> (default) or <c>Mock</c>.
    /// </value>
    public string Provider { get; set; } = "Tauon";
}
