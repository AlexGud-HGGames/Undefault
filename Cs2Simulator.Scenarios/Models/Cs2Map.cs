using System.Text.Json.Serialization;

namespace Cs2Simulator.Scenarios.Models;

public sealed record Cs2Map
{
    [JsonPropertyName("mode")]
    public string? Mode { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("phase")]
    public string? Phase { get; init; }

    [JsonPropertyName("round")]
    public int? Round { get; init; }

    [JsonPropertyName("matchid")]
    public string? MatchId { get; init; }

    [JsonPropertyName("team_ct")]
    public Cs2TeamScore? TeamCt { get; init; }

    [JsonPropertyName("team_t")]
    public Cs2TeamScore? TeamT { get; init; }

    [JsonPropertyName("num_matches_to_win_series")]
    public int? NumMatchesToWinSeries { get; init; }

    [JsonPropertyName("current_spectators")]
    public int? CurrentSpectators { get; init; }

    [JsonPropertyName("souvenirs_total")]
    public int? SouvenirsTotal { get; init; }
}
