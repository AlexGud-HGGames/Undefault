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
            EnableRoundStart = false,
            EnableDeath = true,
            EnableCombat = false,
            EnableIdle = false,
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

        events.Should().ContainSingle(e => e.Type == EventType.Death && e.EventKey == EventKeys.Death);

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
            EnableRoundStart = false,
            EnableDeath = false,
            EnableCombat = true,
            EnableIdle = false,
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

        events.Should().ContainSingle(e => e.Type == EventType.Combat && e.EventKey == EventKeys.Combat);
    }

    [Fact]
    public void Detect_EmitsIdle_WhenNoActivityAfterDebounce()
    {
        var options = new EventDetectorOptions
        {
            EnableRoundStart = false,
            EnableDeath = false,
            EnableCombat = false,
            EnableIdle = true,
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

        events.Should().ContainSingle(e => e.Type == EventType.Idle && e.EventKey == EventKeys.Idle);
    }

    [Fact]
    public void Detect_EmitsRoundStart_WhenPhaseTurnsLive()
    {
        var options = new EventDetectorOptions
        {
            EnableRoundStart = true,
            EnableDeath = false,
            EnableCombat = false,
            EnableIdle = false,
            RoundStartPhase = "live"
        };
        var detector = new EventDetector(options);
        var differ = new SnapshotDiffer();
        var t0 = DateTimeOffset.UtcNow;

        var previous = BuildSnapshot(t0, health: 100, isAlive: true, round: 2, phase: "freezetime");
        var current = BuildSnapshot(t0.AddSeconds(1), health: 100, isAlive: true, round: 2, phase: "live");

        var diff = differ.Compute(previous, current);
        var events = detector.Detect(diff);

        events.Should().ContainSingle(e => e.Type == EventType.RoundStart && e.EventKey == EventKeys.RoundStart);
    }

    private static GameSnapshot BuildSnapshot(
        DateTimeOffset timestamp,
        int health,
        bool isAlive,
        bool inCombat = false,
        int? round = null,
        string? phase = null)
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
                new CombatModule(InCombatHint: inCombat, LastDamageDealtAt: null, LastDamageReceivedAt: null),
                new RoundModule(Round: round, Phase: phase)
            }
        );
    }
}
