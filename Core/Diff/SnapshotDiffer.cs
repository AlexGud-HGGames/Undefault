using Core.Models;

namespace Core.Diff;

public sealed class SnapshotDiffer
{
    private readonly float _movementThreshold;

    public SnapshotDiffer(float movementThreshold = 0.01f)
    {
        _movementThreshold = movementThreshold;
    }

    public SnapshotDiff Compute(GameSnapshot? previous, GameSnapshot current)
    {
        if (current is null)
        {
            throw new ArgumentNullException(nameof(current));
        }

        var previousVitals = previous?.GetModule<VitalsModule>();
        var currentVitals = current.GetModule<VitalsModule>();
        var previousPosition = previous?.GetModule<PositionModule>();
        var currentPosition = current.GetModule<PositionModule>();
        var previousCombat = previous?.GetModule<CombatModule>();
        var currentCombat = current.GetModule<CombatModule>();

        var previousHealth = previousVitals?.Health ?? 0;
        var currentHealth = currentVitals?.Health ?? 0;
        var previousArmor = previousVitals?.Armor ?? 0;
        var currentArmor = currentVitals?.Armor ?? 0;
        var previousIsAlive = previousVitals?.IsAlive ?? false;
        var currentIsAlive = currentVitals?.IsAlive ?? false;

        var previousPositionVector = previousPosition?.Position ?? Vector3.Zero;
        var currentPositionVector = currentPosition?.Position ?? Vector3.Zero;
        var distanceMoved = previousPositionVector.DistanceTo(currentPositionVector);

        var previousIsMoving = previousPosition?.IsMoving ?? false;
        var currentIsMoving = currentPosition?.IsMoving ?? false;
        var previousInCombatHint = previousCombat?.InCombatHint ?? false;
        var currentInCombatHint = currentCombat?.InCombatHint ?? false;

        var didDealDamage = IsNewer(previousCombat?.LastDamageDealtAt, currentCombat?.LastDamageDealtAt);
        var didReceiveDamage = IsNewer(previousCombat?.LastDamageReceivedAt, currentCombat?.LastDamageReceivedAt)
            || currentHealth < previousHealth
            || currentArmor < previousArmor;

        var hasActivity = currentIsMoving
            || currentInCombatHint
            || didDealDamage
            || didReceiveDamage
            || distanceMoved >= _movementThreshold;

        var activityDiff = new ActivityDiff(
            PreviousIsAlive: previousIsAlive,
            CurrentIsAlive: currentIsAlive,
            IsAliveChanged: previousIsAlive != currentIsAlive,
            HealthDelta: currentHealth - previousHealth,
            ArmorDelta: currentArmor - previousArmor,
            DistanceMoved: distanceMoved,
            IsMovingChanged: previousIsMoving != currentIsMoving,
            InCombatHintChanged: previousInCombatHint != currentInCombatHint,
            DidDealDamage: didDealDamage,
            DidReceiveDamage: didReceiveDamage,
            HasActivity: hasActivity
        );

        return new SnapshotDiff(previous, current, activityDiff);
    }

    private static bool IsNewer(DateTimeOffset? previous, DateTimeOffset? current)
    {
        if (!current.HasValue)
        {
            return false;
        }

        if (!previous.HasValue)
        {
            return true;
        }

        return current > previous;
    }
}
