using Core.Adapters;
using Core.Models;
using Core.Music;

namespace Core.Rules;

public sealed class EventDetector
{
    private readonly EventDetectorOptions _options;
    private DateTimeOffset? _lastDeathAt;
    private DateTimeOffset? _lastCombatAt;
    private DateTimeOffset? _lastIdleAt;
    private DateTimeOffset? _combatDebounceStartedAt;
    private DateTimeOffset? _idleDebounceStartedAt;
    private DateTimeOffset? _lastActivityAt;

    public EventDetector(EventDetectorOptions? options = null)
    {
        _options = options ?? new EventDetectorOptions();
    }

    public IReadOnlyList<NormalizedEvent> Detect(NeutralDetectorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var events = new List<NormalizedEvent>();
        var current = context.Current;
        var snapshot = current.Raw;
        var timestamp = current.Clock.WallTimeUtc;

        if (context.IsFirstObservation)
        {
            _lastActivityAt = timestamp;
            _combatDebounceStartedAt = null;
            _idleDebounceStartedAt = null;
            return events;
        }

        if (context.Activity.HasActivity)
        {
            _lastActivityAt = timestamp;
        }

        if (_options.EnableRoundStart && IsRoundStart(context))
        {
            events.Add(NormalizedEvent.RoundStart(snapshot, BuildRoundStartDetail(current.Clock)));
        }

        if (_options.EnableDeath && IsDeath(context) && IsPastCooldown(_lastDeathAt, timestamp, _options.DeathCooldown))
        {
            events.Add(NormalizedEvent.Death(snapshot));
            _lastDeathAt = timestamp;
        }

        if (_options.EnableCombat && IsCombatCondition(context))
        {
            _combatDebounceStartedAt ??= timestamp;

            if (IsPastCooldown(_lastCombatAt, timestamp, _options.CombatCooldown)
                && IsPastDebounce(_combatDebounceStartedAt, timestamp, _options.CombatDebounce))
            {
                events.Add(NormalizedEvent.Combat(snapshot));
                _lastCombatAt = timestamp;
                _combatDebounceStartedAt = timestamp;
            }
        }
        else
        {
            _combatDebounceStartedAt = null;
        }

        if (_options.EnableIdle && IsIdleCondition(context))
        {
            _idleDebounceStartedAt ??= timestamp;

            if (IsPastCooldown(_lastIdleAt, timestamp, _options.IdleCooldown)
                && IsPastDebounce(_idleDebounceStartedAt, timestamp, _options.IdleDebounce))
            {
                var idleDuration = timestamp - (_lastActivityAt ?? timestamp);
                events.Add(NormalizedEvent.Idle(snapshot, idleDuration));
                _lastIdleAt = timestamp;
                _idleDebounceStartedAt = timestamp;
            }
        }
        else
        {
            _idleDebounceStartedAt = null;
        }

        return events;
    }

    public void Reset()
    {
        _lastDeathAt = null;
        _lastCombatAt = null;
        _lastIdleAt = null;
        _combatDebounceStartedAt = null;
        _idleDebounceStartedAt = null;
        _lastActivityAt = null;
    }

    private static bool IsDeath(NeutralDetectorContext context)
    {
        var prev = context.Previous?.Neutral.IsAlive;
        var curr = context.Current.Neutral.IsAlive;
        return prev == true && curr == false;
    }

    // Neutral round-start: a non-Live -> Live phase transition, or a round-index increment.
    // Either signal alone is sufficient (different titles publish them at different cadences).
    private static bool IsRoundStart(NeutralDetectorContext context)
    {
        var currentClock = context.Current.Clock;
        var previousClock = context.Previous?.Clock;

        var phaseWentLive = currentClock.MatchPhase == MatchPhaseNeutral.Live
            && previousClock?.MatchPhase != MatchPhaseNeutral.Live;

        var roundIncremented = previousClock is { RoundIndex: int prevRound }
            && currentClock.RoundIndex is int currRound
            && currRound > prevRound;

        return phaseWentLive || roundIncremented;
    }

    // Combat / idle remain on the raw activity diff and module reads. Neutralizing them is
    // tracked separately; this detector still emits combat/idle in the same way it did before.
    private bool IsCombatCondition(NeutralDetectorContext context)
    {
        var combat = context.Current.Raw.GetModule<CombatModule>();
        return context.Activity.DidDealDamage
            || context.Activity.DidReceiveDamage
            || (combat?.InCombatHint ?? false);
    }

    private bool IsIdleCondition(NeutralDetectorContext context)
    {
        if (context.Current.Neutral.IsAlive != true)
        {
            return false;
        }

        var snapshot = context.Current.Raw;
        var position = snapshot.GetModule<PositionModule>();
        var isMoving = (position?.IsMoving ?? false)
            || context.Activity.DistanceMoved >= _options.MovementThreshold;

        if (isMoving || IsCombatCondition(context))
        {
            return false;
        }

        var timestamp = context.Current.Clock.WallTimeUtc;
        _lastActivityAt ??= timestamp;
        var idleDuration = timestamp - _lastActivityAt.Value;

        return idleDuration >= _options.IdleDebounce;
    }

    private static bool IsPastCooldown(DateTimeOffset? lastEventAt, DateTimeOffset now, TimeSpan cooldown)
    {
        return !lastEventAt.HasValue || now - lastEventAt.Value >= cooldown;
    }

    private static bool IsPastDebounce(DateTimeOffset? startedAt, DateTimeOffset now, TimeSpan debounce)
    {
        return startedAt.HasValue && now - startedAt.Value >= debounce;
    }

    private static string? BuildRoundStartDetail(GameClockSnapshot clock)
    {
        return clock.RoundIndex is int round ? $"round={round}" : null;
    }
}
