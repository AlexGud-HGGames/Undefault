namespace Core.Music;

public interface IMusicMixer
{
    /// <summary>
    /// Produces merged output. Transport here is minimal; full transport table lives in specs.
    /// </summary>
    MergedAudioOutput Merge(IReadOnlyList<IAudioIntent> intents, MusicMixerContext context);
}
