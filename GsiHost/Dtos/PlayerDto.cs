using System.Text.Json;
using System.Text.Json.Serialization;

namespace GsiHost.Dtos;

public sealed class PlayerDto
{
    [JsonPropertyName("steamid")]
    public string? SteamId { get; init; }

    [JsonPropertyName("activity")]
    public string? Activity { get; init; }

    [JsonPropertyName("team")]
    public string? Team { get; init; }

    [JsonPropertyName("position")]
    public JsonElement? Position { get; init; }

    [JsonPropertyName("state")]
    public PlayerStateDto? State { get; init; }
}
