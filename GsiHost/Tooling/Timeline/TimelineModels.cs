using Core.Models;

namespace GsiHost.Tooling.Timeline;

public static class TimelineSources
{
    public const string Gsi = "gsi";
    public const string UserAction = "user_action";
    public const string Playback = "playback";

    /// <summary>Dota 2 GSI events (UND-80). Logging-only source; no adapter/rules engine yet.</summary>
    public const string Dota = "dota";
}

/// <summary>
/// Event keys for Dota 2 GSI transitions recorded to the timeline by
/// <see cref="GsiHost.Services.DotaGsiLoggingService"/>. This is a minimal logging slice
/// (UND-80): there is no neutral-context mapping or rules-engine integration yet.
/// </summary>
public static class TimelineDotaEvents
{
    /// <summary>Raw <c>map.game_state</c> changed (e.g. hero selection -> game in progress).</summary>
    public const string GameStateChanged = "dota_game_state_changed";

    /// <summary>Observed hero transitioned from alive to dead.</summary>
    public const string HeroDied = "dota_hero_died";

    /// <summary>Observed hero transitioned from dead to alive (respawned).</summary>
    public const string HeroRespawned = "dota_hero_respawned";

    /// <summary><c>map.paused</c> transitioned to <see langword="true"/>.</summary>
    public const string Paused = "dota_paused";

    /// <summary><c>map.paused</c> transitioned to <see langword="false"/>.</summary>
    public const string Resumed = "dota_resumed";
}

/// <summary>
/// Event keys for confirmed Spotify playback state transitions recorded to the timeline.
/// </summary>
public static class TimelinePlaybackEvents
{
    /// <summary>Playback transitioned from playing to paused.</summary>
    public const string Paused = "playback_paused";

    /// <summary>Playback transitioned from paused to playing.</summary>
    public const string Resumed = "playback_resumed";
}

public static class TimelineOutcomeStatuses
{
    public const string Received = "received";
    public const string Applied = "applied";
    public const string NoMatchingRule = "no_matching_rule";
    public const string Disabled = "disabled";
    public const string Invalid = "invalid";
    public const string Failed = "failed";
}

public sealed record TimelineCommandOutcome(
    string Status,
    string? Command = null,
    string? Message = null);

public sealed record TimelineGameContext(
    string? GameId,
    string? MatchId,
    string? PlayerId,
    bool? IsAlive,
    int? Health,
    int? Armor,
    int? Round,
    string? RoundPhase,
    bool? InCombatHint,
    DateTimeOffset? LastSnapshotAt,
    IReadOnlyList<string> RecentEventKeys)
{
    public static TimelineGameContext Empty { get; } = new(
        GameId: null,
        MatchId: null,
        PlayerId: null,
        IsAlive: null,
        Health: null,
        Armor: null,
        Round: null,
        RoundPhase: null,
        InCombatHint: null,
        LastSnapshotAt: null,
        RecentEventKeys: Array.Empty<string>());

    public static TimelineGameContext FromSnapshot(
        GameSnapshot? snapshot,
        IReadOnlyList<string>? recentEventKeys = null)
    {
        if (snapshot is null)
        {
            return Empty with
            {
                RecentEventKeys = recentEventKeys ?? Array.Empty<string>()
            };
        }

        var vitals = snapshot.GetModule<VitalsModule>();
        var round = snapshot.GetModule<RoundModule>();
        var combat = snapshot.GetModule<CombatModule>();

        return new TimelineGameContext(
            snapshot.GameId,
            snapshot.MatchId,
            snapshot.PlayerId,
            vitals?.IsAlive,
            vitals?.Health,
            vitals?.Armor,
            round?.Round,
            round?.Phase,
            combat?.InCombatHint,
            snapshot.Timestamp,
            recentEventKeys ?? Array.Empty<string>());
    }
}

public sealed record TimelineEntry(
    long Sequence,
    DateTimeOffset TimestampUtc,
    string Source,
    string EventKey,
    string? Action,
    string? Detail,
    TimelineGameContext GameContext,
    TimelineCommandOutcome? Outcome);

public sealed record IntentEpisode(
    TimelineEntry Label,
    IReadOnlyList<TimelineEntry> Before,
    IReadOnlyList<TimelineEntry> After);
