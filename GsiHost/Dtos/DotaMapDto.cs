using System.Text.Json.Serialization;

namespace GsiHost.Dtos;

public sealed class DotaMapDto
{
    [JsonPropertyName("matchid")]
    public string? MatchId { get; init; }

    [JsonPropertyName("game_time")]
    public int? GameTime { get; init; }

    [JsonPropertyName("clock_time")]
    public int? ClockTime { get; init; }

    /// <summary>Raw <c>DOTA_GameState</c> string, e.g. <c>DOTA_GAMERULES_STATE_GAME_IN_PROGRESS</c>.</summary>
    [JsonPropertyName("game_state")]
    public string? GameState { get; init; }

    [JsonPropertyName("paused")]
    public bool? Paused { get; init; }

    [JsonPropertyName("win_team")]
    public string? WinTeam { get; init; }
}
