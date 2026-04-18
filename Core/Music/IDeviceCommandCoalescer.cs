namespace Core.Music;

/// <summary>
/// Normal-path coalescing before Spotify; emergency lane bypasses this. See docs/stability-and-device-layer-spec.md.
/// </summary>
public interface IDeviceCommandCoalescer
{
    /// <summary>
    /// Returns null if the command should be skipped (within epsilon / min interval).
    /// </summary>
    MergedAudioOutput? TryEmit(MergedAudioOutput candidate, DateTimeOffset nowUtc);
}
