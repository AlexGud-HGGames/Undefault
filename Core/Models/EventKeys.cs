namespace Core.Models;

public static class EventKeys
{
    public const string RoundStart = "round_start";
    public const string Death = "death";
    public const string Combat = "combat";
    public const string Idle = "idle";

    public static string FromType(EventType eventType)
    {
        return eventType switch
        {
            EventType.RoundStart => RoundStart,
            EventType.Death => Death,
            EventType.Combat => Combat,
            EventType.Idle => Idle,
            _ => eventType.ToString().ToLowerInvariant()
        };
    }

    public static string Normalize(string? eventKey)
    {
        return string.IsNullOrWhiteSpace(eventKey)
            ? string.Empty
            : eventKey.Trim().ToLowerInvariant();
    }
}
