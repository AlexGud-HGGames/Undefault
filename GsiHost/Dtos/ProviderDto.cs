using System.Text.Json.Serialization;

namespace GsiHost.Dtos;

public sealed class ProviderDto
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("appid")]
    public int? AppId { get; init; }

    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; init; }
}
