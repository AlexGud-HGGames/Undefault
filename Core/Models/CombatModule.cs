namespace Core.Models;

public sealed record CombatModule(
    bool InCombatHint,
    DateTimeOffset? LastDamageDealtAt,
    DateTimeOffset? LastDamageReceivedAt
) : ISnapshotModule;
