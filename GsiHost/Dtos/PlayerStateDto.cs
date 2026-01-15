using System.Text.Json.Serialization;

namespace GsiHost.Dtos;

public sealed class PlayerStateDto
{
    [JsonPropertyName("health")]
    public int? Health { get; init; }

    [JsonPropertyName("armor")]
    public int? Armor { get; init; }
}
