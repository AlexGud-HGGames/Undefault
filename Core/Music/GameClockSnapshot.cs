namespace Core.Music;

/// <summary>
/// Authoritative time and match phase for envelopes. See docs/neutral-signals-and-game-clock.md.
/// </summary>
public enum MatchPhaseNeutral
{
    Unknown = 0,
    PreLive = 1,
    Live = 2,
    Intermission = 3,
    PostMatch = 4,
}

public readonly record struct GameClockSnapshot(
    DateTimeOffset WallTimeUtc,
    double? GameTimeSeconds,
    bool IsGamePaused,
    MatchPhaseNeutral MatchPhase,
    int? RoundIndex);
