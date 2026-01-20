using Core.Diff;
using Core.Models;
using Core.Rules;
using FluentAssertions;

namespace Core.Tests;

public class EventDetectorTests
{
    [Fact]
    public void Detect_EmitsDeathAndRespectsCooldown()
    {
        var options = new EventDetectorOptions
        {
            DeathCooldown = TimeSpan.FromSeconds(5),
            CombatDebounce = TimeSpan.Zero,
            IdleDebounce = TimeSpan.Zero
        };
        var detector = new EventDetector(options);
        var differ = new SnapshotDiffer();
        var t0 = DateTimeOffset.UtcNow;

        var previous = BuildSnapshot(t0, health: 100, isAlive: true);
        var current = BuildSnapshot(t0.AddSeconds(1), health: 0, isAlive: false);

        var diff = differ.Compute(previous, current);
        var events = detector.Detect(diff);

        events.Should().ContainSingle(e => e.Type == EventType.Death);

        var next = BuildSnapshot(t0.AddSeconds(2), health: 0, isAlive: false);
        var nextDiff = differ.Compute(current, next);
        var nextEvents = detector.Detect(nextDiff);

        nextEvents.Should().NotContain(e => e.Type == EventType.Death);
    }

    [Fact]
    public void Detect_EmitsCombat_WhenCombatHintIsTrue()
    {
        var options = new EventDetectorOptions
        {
            CombatCooldown = TimeSpan.Zero,
            CombatDebounce = TimeSpan.Zero
        };
        var detector = new EventDetector(options);
        var differ = new SnapshotDiffer();
        var t0 = DateTimeOffset.UtcNow;

        var previous = BuildSnapshot(t0, health: 100, isAlive: true, inCombat: false);
        var current = BuildSnapshot(t0.AddSeconds(1), health: 100, isAlive: true, inCombat: true);

        var diff = differ.Compute(previous, current);
        var events = detector.Detect(diff);

        events.Should().ContainSingle(e => e.Type == EventType.Combat);
    }

    [Fact]
    public void Detect_EmitsIdle_WhenNoActivityAfterDebounce()
    {
        var options = new EventDetectorOptions
        {
            IdleCooldown = TimeSpan.Zero,
            IdleDebounce = TimeSpan.Zero
        };
        var detector = new EventDetector(options);
        var differ = new SnapshotDiffer();
        var t0 = DateTimeOffset.UtcNow;

        var previous = BuildSnapshot(t0, health: 100, isAlive: true);
        var current = BuildSnapshot(t0.AddSeconds(1), health: 100, isAlive: true);

        var diff = differ.Compute(previous, current);
        var events = detector.Detect(diff);

        events.Should().ContainSingle(e => e.Type == EventType.Idle);
    }

    private static GameSnapshot BuildSnapshot(
        DateTimeOffset timestamp,
        int health,
        bool isAlive,
        bool inCombat = false)
    {
        return new GameSnapshot(
            Timestamp: timestamp,
            GameId: "cs2",
            MatchId: "match",
            PlayerId: "player",
            Modules: new ISnapshotModule[]
            {
                new VitalsModule(Health: health, Armor: 0, IsAlive: isAlive),
                new PositionModule(Position: Vector3.Zero, IsMoving: false),
                new CombatModule(InCombatHint: inCombat, LastDamageDealtAt: null, LastDamageReceivedAt: null)
            }
        );
    }
}
