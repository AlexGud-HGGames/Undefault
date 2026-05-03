using Core.Models;
using FluentAssertions;

namespace Core.Tests;

public sealed class TimelineGameContextTests
{
    [Fact]
    public void FromSnapshot_FlattensRelevantGameplayContext()
    {
        var snapshot = new GameSnapshot(
            Timestamp: DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
            GameId: "cs2",
            MatchId: "match",
            PlayerId: "player",
            Modules: new ISnapshotModule[]
            {
                new VitalsModule(Health: 42, Armor: 80, IsAlive: true),
                new RoundModule(Round: 12, Phase: "live"),
                new CombatModule(InCombatHint: true, LastDamageDealtAt: null, LastDamageReceivedAt: null)
            });

        var context = TimelineGameContext.FromSnapshot(snapshot, new[] { EventKeys.RoundStart });

        context.GameId.Should().Be("cs2");
        context.MatchId.Should().Be("match");
        context.PlayerId.Should().Be("player");
        context.IsAlive.Should().BeTrue();
        context.Health.Should().Be(42);
        context.Armor.Should().Be(80);
        context.Round.Should().Be(12);
        context.RoundPhase.Should().Be("live");
        context.InCombatHint.Should().BeTrue();
        context.LastSnapshotAt.Should().Be(snapshot.Timestamp);
        context.RecentEventKeys.Should().ContainSingle(EventKeys.RoundStart);
    }
}
