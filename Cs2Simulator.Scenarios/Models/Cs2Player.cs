using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cs2Simulator.Scenarios.Models;

public sealed record Cs2Player
{
    [JsonPropertyName("steamid")]
    public string? SteamId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("observer_slot")]
    public int? ObserverSlot { get; init; }

    [JsonPropertyName("team")]
    public string? Team { get; init; }

    [JsonPropertyName("activity")]
    public string? Activity { get; init; }

    [JsonPropertyName("position")]
    public string? Position { get; init; }

    [JsonPropertyName("forward")]
    public string? Forward { get; init; }

    [JsonPropertyName("state")]
    public Cs2PlayerState? State { get; init; }

    [JsonPropertyName("weapons")]
    public IReadOnlyDictionary<string, Cs2Weapon>? Weapons { get; init; }

    [JsonPropertyName("match_stats")]
    public Cs2MatchStats? MatchStats { get; init; }
}
