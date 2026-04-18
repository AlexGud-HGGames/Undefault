namespace Core.Music;

/// <summary>
/// Multiplicative gain in (0, 1], combined per volume-composition-spec.
/// </summary>
public sealed class GainAudioIntent : IAudioIntent
{
    public GainAudioIntent(string channelId, int priority, float gain)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            throw new ArgumentException("Channel id is required.", nameof(channelId));
        }

        if (gain <= 0f || gain > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(gain), gain, "Gain must be in (0, 1].");
        }

        ChannelId = channelId;
        Priority = priority;
        Gain = gain;
    }

    public string ChannelId { get; }

    public int Priority { get; }

    public float Gain { get; }
}
