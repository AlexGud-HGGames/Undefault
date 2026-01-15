namespace Core.Diff;

public sealed record ActivityDiff(
    bool PreviousIsAlive,
    bool CurrentIsAlive,
    bool IsAliveChanged,
    int HealthDelta,
    int ArmorDelta,
    float DistanceMoved,
    bool IsMovingChanged,
    bool InCombatHintChanged,
    bool DidDealDamage,
    bool DidReceiveDamage,
    bool HasActivity
);
