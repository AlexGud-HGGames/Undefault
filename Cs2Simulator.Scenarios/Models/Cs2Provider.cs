using System.Text.Json.Serialization;

namespace Cs2Simulator.Scenarios.Models;

public sealed record Cs2Provider
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("appid")]
    public int? AppId { get; init; }

    [JsonPropertyName("version")]
    public int? Version { get; init; }

    [JsonPropertyName("steamid")]
    public string? SteamId { get; init; }

    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; init; }
}
