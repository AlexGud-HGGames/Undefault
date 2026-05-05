using Core.Models;

namespace GsiHost.Tooling.Timeline;

public static class TimelineSources
{
    public const string Gsi = "gsi";
    public const string UserAction = "user_action";
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
