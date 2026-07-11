using System.Text.Json.Serialization;

namespace GsiHost.Dtos;

public sealed class DotaPlayerDto
{
    [JsonPropertyName("steamid")]
    public string? SteamId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("kills")]
    public int? Kills { get; init; }

    [JsonPropertyName("deaths")]
    public int? Deaths { get; init; }

    [JsonPropertyName("assists")]
    public int? Assists { get; init; }
}
