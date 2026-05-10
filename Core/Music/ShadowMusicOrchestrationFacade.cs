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

        var intent = BuildShadowIntent(state, reason);
        var predictedVolume = PredictMergedVolumePercent(intent);

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
            LastMusicIntent = intent,
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

    // Translate the resolved shadow safety state into a MusicIntent that, when run through
    // PredictMergedVolumePercent, reproduces the legacy baseline volumes:
    //   Safe    -> 100 (no constraints)
    //   Unknown -> 30  (conservative ceiling)
    //   Danger  -> 0   (PreferSilence + floor/ceiling pinned to 0)
    // Floor/ceiling semantics here mirror docs/volume-composition-spec.md: ceiling is the
    // upper cap; in Danger, floor is forbidden so we pin both to 0.
    private static MusicIntent BuildShadowIntent(MusicSafetyState state, string reason)
    {
        return state switch
        {
            MusicSafetyState.Danger => MusicIntent.Create(
                transportIntent: TransportIntentNeutral.PreferSilence,
                floorVolumePercent: 0,
                ceilingVolumePercent: 0,
                reason: reason),
            MusicSafetyState.Unknown => MusicIntent.Create(
                ceilingVolumePercent: DefaultFloorVolumePercent,
                reason: reason),
            _ => MusicIntent.Create(reason: reason),
        };
    }

    private static int PredictMergedVolumePercent(MusicIntent intent)
    {
        if (intent.TransportIntent == TransportIntentNeutral.PreferSilence)
        {
            return 0;
        }

        var volume = DefaultBaseVolumePercent;
        if (intent.FloorVolumePercent.HasValue)
        {
            volume = Math.Max(volume, intent.FloorVolumePercent.Value);
        }
        if (intent.CeilingVolumePercent.HasValue)
        {
            volume = Math.Min(volume, intent.CeilingVolumePercent.Value);
        }
        return volume;
    }
}
