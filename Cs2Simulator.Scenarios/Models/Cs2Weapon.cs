using System.Text.Json.Serialization;

namespace Cs2Simulator.Scenarios.Models;

public sealed record Cs2Weapon
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("paintkit")]
    public string? PaintKit { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("ammo_clip")]
    public int? AmmoClip { get; init; }

    [JsonPropertyName("ammo_clip_max")]
    public int? AmmoClipMax { get; init; }

    [JsonPropertyName("ammo_reserve")]
    public int? AmmoReserve { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }
}
