namespace GsiHost.Players;

/// <summary>
/// Binds the <c>Tauon</c> section used by <see cref="TauonMusicPlayer"/>.
/// </summary>
public sealed class TauonOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Tauon";

    /// <summary>
    /// Gets or sets the Tauon remote-control origin.
    /// </summary>
    /// <value>
    /// The loopback base URL with no trailing path. The default is <c>http://127.0.0.1:7814</c>.
    /// </value>
    public string BaseUrl { get; set; } = "http://127.0.0.1:7814";

    /// <summary>
    /// Gets or sets the HTTP timeout, in seconds, applied to the adapter <see cref="HttpClient"/>.
    /// </summary>
    /// <value>
    /// The timeout in seconds. The default is 2. Values below 1 are clamped to 1 by
    /// <see cref="TauonMusicPlayer.ConfigureClient"/>.
    /// </value>
    public int TimeoutSeconds { get; set; } = 2;
}
