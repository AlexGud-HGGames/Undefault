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

        var previousPlayer = previous?.Player ?? PlayerSnapshot.Empty;
        var currentPlayer = current.Player;

        var distanceMoved = previousPlayer.Position.DistanceTo(currentPlayer.Position);
        var didDealDamage = IsNewer(previousPlayer.LastDamageDealtAt, currentPlayer.LastDamageDealtAt);
        var didReceiveDamage = IsNewer(previousPlayer.LastDamageReceivedAt, currentPlayer.LastDamageReceivedAt);

        var hasActivity = currentPlayer.IsMoving
            || currentPlayer.InCombatHint
            || didDealDamage
            || didReceiveDamage
            || distanceMoved >= _movementThreshold;

        var playerDiff = new PlayerDiff(
            PreviousIsAlive: previousPlayer.IsAlive,
            CurrentIsAlive: currentPlayer.IsAlive,
            IsAliveChanged: previousPlayer.IsAlive != currentPlayer.IsAlive,
            HealthDelta: currentPlayer.Health - previousPlayer.Health,
            ArmorDelta: currentPlayer.Armor - previousPlayer.Armor,
            DistanceMoved: distanceMoved,
            IsMovingChanged: previousPlayer.IsMoving != currentPlayer.IsMoving,
            InCombatHintChanged: previousPlayer.InCombatHint != currentPlayer.InCombatHint,
            DidDealDamage: didDealDamage,
            DidReceiveDamage: didReceiveDamage,
            HasActivity: hasActivity
        );

        return new SnapshotDiff(previous, current, playerDiff);
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
