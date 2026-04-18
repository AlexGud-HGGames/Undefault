namespace Core.Music;

/// <summary>
/// Reference mixer implementing the v1 multiplicative gain pipeline from docs/volume-composition-spec.md.
/// </summary>
public sealed class DefaultMusicMixer : IMusicMixer
{
    public MergedAudioOutput Merge(IReadOnlyList<IAudioIntent> intents, MusicMixerContext context)
    {
        if (context.SafetyState == MusicSafetyState.Danger)
        {
            return new MergedAudioOutput(
                TargetVolumePercent: context.ForbidFloorInDanger ? 0 : context.FloorVolumePercent ?? 0,
                Transport: TransportCommandKind.Pause,
                HardSuppressAudio: true);
        }

        if (context.SafetyState == MusicSafetyState.Unknown)
        {
            var conservative = context.FloorVolumePercent ?? 0;
            conservative = Math.Clamp(conservative, 0, context.CeilingVolumePercent);
            return new MergedAudioOutput(conservative, TransportCommandKind.NoChange, HardSuppressAudio: false);
        }

        var product = 1f;
        foreach (var intent in intents)
        {
            if (intent is GainAudioIntent g)
            {
                product *= g.Gain;
            }
        }

        var raw = context.BaseVolumePercent * product;
        var floor = context.FloorVolumePercent ?? 0;
        var clamped = (int)Math.Round(Math.Clamp(raw, floor, context.CeilingVolumePercent));
        return new MergedAudioOutput(clamped, TransportCommandKind.NoChange, HardSuppressAudio: false);
    }
}
