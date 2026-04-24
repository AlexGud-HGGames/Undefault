using System.Text.Json.Serialization;

namespace Cs2Simulator.Scenarios.Models;

public sealed record Cs2TeamScore
{
    [JsonPropertyName("score")]
    public int? Score { get; init; }

    [JsonPropertyName("consecutive_round_losses")]
    public int? ConsecutiveRoundLosses { get; init; }

    [JsonPropertyName("timeouts_remaining")]
    public int? TimeoutsRemaining { get; init; }

    [JsonPropertyName("matches_won_this_series")]
    public int? MatchesWonThisSeries { get; init; }
}
