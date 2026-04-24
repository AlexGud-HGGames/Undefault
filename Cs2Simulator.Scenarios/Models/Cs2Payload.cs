using System.Text.Json.Serialization;

namespace Cs2Simulator.Scenarios.Models;

public sealed record Cs2Payload
{
    [JsonPropertyName("provider")]
    public Cs2Provider? Provider { get; init; }

    [JsonPropertyName("map")]
    public Cs2Map? Map { get; init; }

    [JsonPropertyName("round")]
    public Cs2Round? Round { get; init; }

    [JsonPropertyName("player")]
    public Cs2Player? Player { get; init; }
}
