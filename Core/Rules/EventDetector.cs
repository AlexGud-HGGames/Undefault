using Core.Diff;
using Core.Models;

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

    public IReadOnlyList<NormalizedEvent> Detect(SnapshotDiff diff)
    {
        if (diff is null)
        {
            throw new ArgumentNullException(nameof(diff));
        }

        var events = new List<NormalizedEvent>();
        var snapshot = diff.Current;
        var timestamp = snapshot.Timestamp;

        if (diff.IsFirstSnapshot)
        {
            _lastActivityAt = timestamp;
            _combatDebounceStartedAt = null;
            _idleDebounceStartedAt = null;
            return events;
        }

        if (diff.Activity.HasActivity)
        {
            _lastActivityAt = timestamp;
        }

        if (IsDeath(diff) && IsPastCooldown(_lastDeathAt, timestamp, _options.DeathCooldown))
        {
            events.Add(NormalizedEvent.Death(snapshot));
            _lastDeathAt = timestamp;
        }

        if (IsCombatCondition(diff, snapshot))
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

        if (IsIdleCondition(diff, snapshot))
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

    private bool IsDeath(SnapshotDiff diff)
    {
        return diff.Activity.PreviousIsAlive && !diff.Activity.CurrentIsAlive;
    }

    private bool IsCombatCondition(SnapshotDiff diff, GameSnapshot snapshot)
    {
        var combat = snapshot.GetModule<CombatModule>();
        return diff.Activity.DidDealDamage
            || diff.Activity.DidReceiveDamage
            || (combat?.InCombatHint ?? false);
    }

    private bool IsIdleCondition(SnapshotDiff diff, GameSnapshot snapshot)
    {
        var vitals = snapshot.GetModule<VitalsModule>();
        if (!(vitals?.IsAlive ?? false))
        {
            return false;
        }

        var position = snapshot.GetModule<PositionModule>();
        var isMoving = (position?.IsMoving ?? false)
            || diff.Activity.DistanceMoved >= _options.MovementThreshold;

        if (isMoving || IsCombatCondition(diff, snapshot))
        {
            return false;
        }

        _lastActivityAt ??= snapshot.Timestamp;
        var idleDuration = snapshot.Timestamp - _lastActivityAt.Value;

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
}
