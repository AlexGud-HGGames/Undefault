using System.Text.Json.Serialization;

namespace Cs2Simulator.Scenarios.Models;

public sealed record Cs2Round
{
    [JsonPropertyName("phase")]
    public string? Phase { get; init; }

    [JsonPropertyName("bomb")]
    public string? Bomb { get; init; }

    [JsonPropertyName("win_team")]
    public string? WinTeam { get; init; }
}
