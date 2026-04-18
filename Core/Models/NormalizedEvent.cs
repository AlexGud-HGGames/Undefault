namespace Core.Models;

public sealed record NormalizedEvent(
    EventType Type,
    string EventKey,
    DateTimeOffset Timestamp,
    EventContext Context,
    TimeSpan? Duration,
    string? Detail
)
{
    public static NormalizedEvent Death(GameSnapshot snapshot, string? detail = null)
    {
        return new NormalizedEvent(
            EventType.Death,
            EventKeys.Death,
            snapshot.Timestamp,
            EventContext.FromSnapshot(snapshot),
            Duration: null,
            Detail: detail
        );
    }

    public static NormalizedEvent RoundStart(GameSnapshot snapshot, string? detail = null)
    {
        return new NormalizedEvent(
            EventType.RoundStart,
            EventKeys.RoundStart,
            snapshot.Timestamp,
            EventContext.FromSnapshot(snapshot),
            Duration: null,
            Detail: detail
        );
    }

    public static NormalizedEvent Combat(GameSnapshot snapshot, string? detail = null)
    {
        return new NormalizedEvent(
            EventType.Combat,
            EventKeys.Combat,
            snapshot.Timestamp,
            EventContext.FromSnapshot(snapshot),
            Duration: null,
            Detail: detail
        );
    }

    public static NormalizedEvent Idle(GameSnapshot snapshot, TimeSpan idleDuration, string? detail = null)
    {
        return new NormalizedEvent(
            EventType.Idle,
            EventKeys.Idle,
            snapshot.Timestamp,
            EventContext.FromSnapshot(snapshot),
            idleDuration,
            detail
        );
    }
}
