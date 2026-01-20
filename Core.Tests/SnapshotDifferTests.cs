using Core.Diff;
using Core.Models;
using FluentAssertions;

namespace Core.Tests;

public class SnapshotDifferTests
{
    [Fact]
    public void Compute_HandlesMissingModules()
    {
        var differ = new SnapshotDiffer();
        var current = new GameSnapshot(
            Timestamp: DateTimeOffset.UtcNow,
            GameId: "cs2",
            MatchId: "match",
            PlayerId: "player",
            Modules: Array.Empty<ISnapshotModule>()
        );

        var diff = differ.Compute(previous: null, current);

        diff.Activity.HasActivity.Should().BeFalse();
        diff.Activity.CurrentIsAlive.Should().BeFalse();
        diff.Activity.HealthDelta.Should().Be(0);
        diff.Activity.ArmorDelta.Should().Be(0);
    }

    [Fact]
    public void Compute_TracksMovementAndDamage()
    {
        var differ = new SnapshotDiffer();
        var t0 = DateTimeOffset.UtcNow;

        var previous = new GameSnapshot(
            Timestamp: t0,
            GameId: "cs2",
            MatchId: "match",
            PlayerId: "player",
            Modules: new ISnapshotModule[]
            {
                new VitalsModule(Health: 100, Armor: 50, IsAlive: true),
                new PositionModule(Position: new Vector3(0, 0, 0), IsMoving: false),
                new CombatModule(InCombatHint: false, LastDamageDealtAt: null, LastDamageReceivedAt: null)
            }
        );

        var current = new GameSnapshot(
            Timestamp: t0.AddSeconds(1),
            GameId: "cs2",
            MatchId: "match",
            PlayerId: "player",
            Modules: new ISnapshotModule[]
            {
                new VitalsModule(Health: 80, Armor: 40, IsAlive: true),
                new PositionModule(Position: new Vector3(10, 0, 0), IsMoving: true),
                new CombatModule(InCombatHint: true, LastDamageDealtAt: null, LastDamageReceivedAt: null)
            }
        );

        var diff = differ.Compute(previous, current);

        diff.Activity.HealthDelta.Should().Be(-20);
        diff.Activity.ArmorDelta.Should().Be(-10);
        diff.Activity.DistanceMoved.Should().BeGreaterThan(0);
        diff.Activity.HasActivity.Should().BeTrue();
        diff.Activity.InCombatHintChanged.Should().BeTrue();
        diff.Activity.DidReceiveDamage.Should().BeTrue();
    }
}
