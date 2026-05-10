using Core.Adapters;

namespace Core.Music;

public sealed class ShadowMusicOrchestrationFacade : IMusicOrchestrationFacade
{
    private const int DefaultBaseVolumePercent = 100;
    private const int DefaultFloorVolumePercent = 30;
    private const int DefaultCeilingVolumePercent = 100;
    private const bool DefaultForbidFloorInDanger = true;

    // Conservative escalation marker: an adapter that reports Unknown while neutral context
    // says the player is dead must not be observed as Safe by downstream controllers.
    public const string DeadFallbackReason = "shadow:dead-fallback";

    public MusicEngineDebugSnapshot EvaluateShadow(AdapterObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        var (state, reason) = ResolveShadowSafety(observation);

        _ = new MusicMixerContext(
            SafetyState: state,
            BaseVolumePercent: DefaultBaseVolumePercent,
            FloorVolumePercent: DefaultFloorVolumePercent,
            CeilingVolumePercent: DefaultCeilingVolumePercent,
            ForbidFloorInDanger: DefaultForbidFloorInDanger);

        var predictedVolume = PredictMergedVolumePercent(state);

        var contributions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["adaptive"] = $"{DefaultBaseVolumePercent}%",
            ["floor"] = $"{DefaultFloorVolumePercent}%",
        };

        return new MusicEngineDebugSnapshot
        {
            CapturedAtUtc = observation.Clock.WallTimeUtc,
            DesiredSafetyState = state,
            LastSafetyTransitionReason = reason,
            Clock = observation.Clock,
            MixerChannelContributions = contributions,
            LastMergedVolumePercent = predictedVolume,
            LastDeviceCommands = null,
            DeviceDegraded = false,
            LastSpotifyError = null,
        };
    }

    private static (MusicSafetyState State, string Reason) ResolveShadowSafety(AdapterObservation observation)
    {
        var safety = observation.Safety;
        var neutral = observation.Neutral;

        if (safety.State == MusicSafetyState.Unknown && neutral.IsAlive == false)
        {
            return (MusicSafetyState.Danger, AppendStaleness(DeadFallbackReason, safety));
        }

        var baseReason = string.IsNullOrWhiteSpace(safety.Reason)
            ? "shadow:propagated"
            : $"shadow:{safety.Reason}";

        return (safety.State, baseReason);
    }

    private static string AppendStaleness(string baseReason, SafetyFacts safety)
    {
        if (!safety.IsStale)
        {
            return baseReason;
        }

        return string.IsNullOrWhiteSpace(safety.Reason)
            ? $"{baseReason};stale"
            : $"{baseReason};stale:{safety.Reason}";
    }

    private static int PredictMergedVolumePercent(MusicSafetyState state)
    {
        return state switch
        {
            MusicSafetyState.Danger => DefaultForbidFloorInDanger ? 0 : DefaultFloorVolumePercent,
            MusicSafetyState.Unknown => Math.Clamp(DefaultFloorVolumePercent, 0, DefaultCeilingVolumePercent),
            MusicSafetyState.Safe => DefaultBaseVolumePercent,
            _ => DefaultFloorVolumePercent,
        };
    }
}
