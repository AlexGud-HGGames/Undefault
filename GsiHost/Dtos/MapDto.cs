using System.Text.Json.Serialization;

namespace GsiHost.Dtos;

public sealed class MapDto
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("phase")]
    public string? Phase { get; init; }

    [JsonPropertyName("round")]
    public int? Round { get; init; }

    [JsonPropertyName("matchid")]
    public string? MatchId { get; init; }
}
