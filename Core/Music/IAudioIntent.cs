namespace Core.Music;

/// <summary>
/// Semantic audio intent before Spotify. See docs/mixer-contract-and-device-wiring.md.
/// </summary>
public interface IAudioIntent
{
    string ChannelId { get; }

    /// <summary>Higher wins on tie for same intent class.</summary>
    int Priority { get; }
}
