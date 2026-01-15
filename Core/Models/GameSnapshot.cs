namespace Core.Models;

public sealed record GameSnapshot(
    DateTimeOffset Timestamp,
    string? GameId,
    string? MatchId,
    PlayerSnapshot Player
);
