namespace Core.Models;

public sealed record EventContext(
    string? GameId,
    string? MatchId,
    string? PlayerId
)
{
    public static EventContext FromSnapshot(GameSnapshot snapshot)
    {
        return new EventContext(
            snapshot.GameId,
            snapshot.MatchId,
            snapshot.Player.PlayerId
        );
    }
}
