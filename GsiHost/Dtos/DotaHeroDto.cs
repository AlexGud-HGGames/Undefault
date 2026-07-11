using System.Text.Json.Serialization;

namespace GsiHost.Dtos;

public sealed class DotaHeroDto
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("level")]
    public int? Level { get; init; }

    [JsonPropertyName("health")]
    public int? Health { get; init; }

    [JsonPropertyName("max_health")]
    public int? MaxHealth { get; init; }

    [JsonPropertyName("alive")]
    public bool? Alive { get; init; }
}
