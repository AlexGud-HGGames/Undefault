namespace Core.Models;

public sealed record NormalizedEvent(
    EventType Type,
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
            snapshot.Timestamp,
            EventContext.FromSnapshot(snapshot),
            idleDuration,
            detail
        );
    }
}
