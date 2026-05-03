namespace Core.Adapters;

public enum TransportIntentNeutral
{
    NoChange = 0,
    PreferPause = 1,
    PreferResume = 2,
    PreferSilence = 3,
}

public sealed record NeutralContext(
    bool? IsAlive,
    float? EngagementPressure,
    float? ObjectivePressure,
    bool? SpectatorOrObserver,
    TransportIntentNeutral TransportIntent,
    DateTimeOffset ObservedAtUtc)
{
    public static NeutralContext Unknown(DateTimeOffset observedAtUtc)
    {
        return new NeutralContext(
            IsAlive: null,
            EngagementPressure: null,
            ObjectivePressure: null,
            SpectatorOrObserver: null,
            TransportIntent: TransportIntentNeutral.NoChange,
            ObservedAtUtc: observedAtUtc);
    }
}
