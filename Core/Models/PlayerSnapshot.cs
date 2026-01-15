namespace Core.Models;

public sealed record PlayerSnapshot(
    string? PlayerId,
    bool IsAlive,
    int Health,
    int Armor,
    Vector3 Position,
    bool IsMoving,
    bool InCombatHint,
    DateTimeOffset? LastDamageDealtAt,
    DateTimeOffset? LastDamageReceivedAt
)
{
    public static PlayerSnapshot Empty { get; } = new(
        PlayerId: null,
        IsAlive: false,
        Health: 0,
        Armor: 0,
        Position: Vector3.Zero,
        IsMoving: false,
        InCombatHint: false,
        LastDamageDealtAt: null,
        LastDamageReceivedAt: null
    );
}
