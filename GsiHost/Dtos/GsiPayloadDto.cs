using System.Text.Json.Serialization;

namespace GsiHost.Dtos;

public sealed class GsiPayloadDto
{
    [JsonPropertyName("provider")]
    public ProviderDto? Provider { get; init; }

    [JsonPropertyName("map")]
    public MapDto? Map { get; init; }

    [JsonPropertyName("player")]
    public PlayerDto? Player { get; init; }
}
