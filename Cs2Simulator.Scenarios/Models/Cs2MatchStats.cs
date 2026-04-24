using System.Text.Json.Serialization;

namespace Cs2Simulator.Scenarios.Models;

public sealed record Cs2MatchStats
{
    [JsonPropertyName("kills")]
    public int? Kills { get; init; }

    [JsonPropertyName("assists")]
    public int? Assists { get; init; }

    [JsonPropertyName("deaths")]
    public int? Deaths { get; init; }

    [JsonPropertyName("mvps")]
    public int? Mvps { get; init; }

    [JsonPropertyName("score")]
    public int? Score { get; init; }
}
