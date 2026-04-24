using System.Text.Json.Serialization;

namespace Cs2Simulator.Scenarios.Models;

public sealed record Cs2PlayerState
{
    [JsonPropertyName("health")]
    public int? Health { get; init; }

    [JsonPropertyName("armor")]
    public int? Armor { get; init; }

    [JsonPropertyName("helmet")]
    public bool? Helmet { get; init; }

    [JsonPropertyName("flashed")]
    public int? Flashed { get; init; }

    [JsonPropertyName("smoked")]
    public int? Smoked { get; init; }

    [JsonPropertyName("burning")]
    public int? Burning { get; init; }

    [JsonPropertyName("money")]
    public int? Money { get; init; }

    [JsonPropertyName("round_kills")]
    public int? RoundKills { get; init; }

    [JsonPropertyName("round_killhs")]
    public int? RoundKillHeadshots { get; init; }

    [JsonPropertyName("round_totaldmg")]
    public int? RoundTotalDamage { get; init; }

    [JsonPropertyName("equip_value")]
    public int? EquipValue { get; init; }

    [JsonPropertyName("defusekit")]
    public bool? DefuseKit { get; init; }
}
