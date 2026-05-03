namespace Core.Adapters;

public sealed record TitleDomainEvent(
    string Key,
    DateTimeOffset Timestamp,
    string? Detail);
