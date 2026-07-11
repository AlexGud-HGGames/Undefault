using System.Text.Json.Serialization;

namespace GsiHost.Dtos;

/// <summary>
/// Minimal Dota 2 GSI payload shape (UND-80). Only the flat, non-spectator player/hero shape is
/// parsed; the <c>player:team#:player#</c> / <c>hero:team#:player#</c> spectator shape documented
/// by Valve is not supported yet. All sections are optional because Dota only announces keys that
/// changed since the last tick.
/// </summary>
public sealed class DotaGsiPayloadDto
{
    [JsonPropertyName("provider")]
    public ProviderDto? Provider { get; init; }

    [JsonPropertyName("map")]
    public DotaMapDto? Map { get; init; }

    [JsonPropertyName("player")]
    public DotaPlayerDto? Player { get; init; }

    [JsonPropertyName("hero")]
    public DotaHeroDto? Hero { get; init; }
}
