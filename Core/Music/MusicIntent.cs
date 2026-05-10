using Core.Adapters;

namespace Core.Music;

/// <summary>
/// Neutral, side-effect-free description of what a scenario wants from the music engine.
/// Scenarios emit <see cref="MusicIntent"/>; the orchestration facade translates merged
/// intents into actual playback commands. Use <see cref="Create"/> for validated
/// construction; <c>with</c>-mutated instances skip validation by design (internal use only).
/// See docs/volume-composition-spec.md for the merge algebra.
/// </summary>
public sealed record MusicIntent
{
    public TransportIntentNeutral TransportIntent { get; init; } = TransportIntentNeutral.NoChange;

    public int? FloorVolumePercent { get; init; }

    public int? CeilingVolumePercent { get; init; }

    public float? GainBias { get; init; }

    public TimeSpan? CooldownHint { get; init; }

    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Permits non-safety relaxations (e.g. cooperating overlay). Has no effect under
    /// <see cref="MusicSafetyState.Danger"/>: safety always wins.
    /// </summary>
    public bool SafetyOverrideAllowed { get; init; }

    public static readonly MusicIntent NoOp = new();

    public static MusicIntent Create(
        TransportIntentNeutral transportIntent = TransportIntentNeutral.NoChange,
        int? floorVolumePercent = null,
        int? ceilingVolumePercent = null,
        float? gainBias = null,
        TimeSpan? cooldownHint = null,
        string? reason = null,
        bool safetyOverrideAllowed = false)
    {
        ValidateVolume(floorVolumePercent, nameof(floorVolumePercent));
        ValidateVolume(ceilingVolumePercent, nameof(ceilingVolumePercent));
        if (floorVolumePercent.HasValue
            && ceilingVolumePercent.HasValue
            && floorVolumePercent.Value > ceilingVolumePercent.Value)
        {
            throw new ArgumentException(
                $"floor ({floorVolumePercent}) must be <= ceiling ({ceilingVolumePercent}) on a single intent",
                nameof(floorVolumePercent));
        }
        if (cooldownHint is { } cd && cd < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(cooldownHint), cd, "cooldown must be non-negative");
        }
        if (gainBias is { } gb && (float.IsNaN(gb) || float.IsInfinity(gb)))
        {
            throw new ArgumentOutOfRangeException(nameof(gainBias), gb, "gain bias must be finite");
        }

        return new MusicIntent
        {
            TransportIntent = transportIntent,
            FloorVolumePercent = floorVolumePercent,
            CeilingVolumePercent = ceilingVolumePercent,
            GainBias = gainBias,
            CooldownHint = cooldownHint,
            Reason = reason ?? string.Empty,
            SafetyOverrideAllowed = safetyOverrideAllowed,
        };
    }

    /// <summary>
    /// Merge a tick's intents into a single resolved intent.
    ///
    /// Precedence (per docs/volume-composition-spec.md):
    /// 1. <see cref="MusicSafetyState.Danger"/> overrides everything: transport=PreferSilence,
    ///    floor=0, ceiling=0, no gain, <see cref="SafetyOverrideAllowed"/> forced false.
    /// 2. Transport: highest rank wins (Silence > Pause > Resume > NoChange).
    /// 3. Floor: max of proposed floors (most "audible insistence").
    /// 4. Ceiling: min of proposed ceilings (most restrictive cap). If floor exceeds ceiling,
    ///    floor is clamped down to ceiling so that floor &lt;= ceiling always holds.
    /// 5. GainBias: sum of contributions, clamped to [-1, +1] to bound dynamic range.
    /// 6. CooldownHint: max (longest hold wins).
    /// 7. Reason: ';'-joined non-empty reasons in input order, deduplicated.
    /// 8. SafetyOverrideAllowed: AND across intents (most conservative).
    /// </summary>
    public static MusicIntent Merge(IEnumerable<MusicIntent> intents, MusicSafetyState safetyState)
    {
        ArgumentNullException.ThrowIfNull(intents);

        var list = intents.Where(i => i is not null).ToList();

        if (safetyState == MusicSafetyState.Danger)
        {
            var reason = ComposeReason("danger", list.Select(i => i.Reason));
            return new MusicIntent
            {
                TransportIntent = TransportIntentNeutral.PreferSilence,
                FloorVolumePercent = 0,
                CeilingVolumePercent = 0,
                GainBias = null,
                CooldownHint = MaxOrNull(list.Select(i => i.CooldownHint)),
                Reason = reason,
                SafetyOverrideAllowed = false,
            };
        }

        if (list.Count == 0)
        {
            return NoOp;
        }

        var transport = list.Aggregate(
            TransportIntentNeutral.NoChange,
            (acc, i) => HigherTransport(acc, i.TransportIntent));

        var floors = list.Where(i => i.FloorVolumePercent.HasValue)
                         .Select(i => i.FloorVolumePercent!.Value)
                         .ToList();
        int? mergedFloor = floors.Count == 0 ? null : floors.Max();

        var ceilings = list.Where(i => i.CeilingVolumePercent.HasValue)
                           .Select(i => i.CeilingVolumePercent!.Value)
                           .ToList();
        int? mergedCeiling = ceilings.Count == 0 ? null : ceilings.Min();

        if (mergedFloor.HasValue && mergedCeiling.HasValue && mergedFloor.Value > mergedCeiling.Value)
        {
            mergedFloor = mergedCeiling;
        }

        float? mergedGain = null;
        var biases = list.Where(i => i.GainBias.HasValue).Select(i => i.GainBias!.Value).ToList();
        if (biases.Count > 0)
        {
            mergedGain = Math.Clamp(biases.Sum(), -1f, 1f);
        }

        return new MusicIntent
        {
            TransportIntent = transport,
            FloorVolumePercent = mergedFloor,
            CeilingVolumePercent = mergedCeiling,
            GainBias = mergedGain,
            CooldownHint = MaxOrNull(list.Select(i => i.CooldownHint)),
            Reason = ComposeReason(null, list.Select(i => i.Reason)),
            SafetyOverrideAllowed = list.All(i => i.SafetyOverrideAllowed),
        };
    }

    private static void ValidateVolume(int? value, string paramName)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "volume percent must be in [0, 100]");
        }
    }

    private static int TransportRank(TransportIntentNeutral t) => t switch
    {
        TransportIntentNeutral.PreferSilence => 3,
        TransportIntentNeutral.PreferPause => 2,
        TransportIntentNeutral.PreferResume => 1,
        _ => 0,
    };

    private static TransportIntentNeutral HigherTransport(TransportIntentNeutral a, TransportIntentNeutral b)
        => TransportRank(a) >= TransportRank(b) ? a : b;

    private static TimeSpan? MaxOrNull(IEnumerable<TimeSpan?> values)
    {
        TimeSpan? max = null;
        foreach (var v in values)
        {
            if (!v.HasValue)
            {
                continue;
            }
            if (!max.HasValue || v.Value > max.Value)
            {
                max = v.Value;
            }
        }
        return max;
    }

    private static string ComposeReason(string? prefix, IEnumerable<string> reasons)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(prefix) && seen.Add(prefix))
        {
            parts.Add(prefix);
        }
        foreach (var r in reasons)
        {
            if (string.IsNullOrWhiteSpace(r))
            {
                continue;
            }
            if (seen.Add(r))
            {
                parts.Add(r);
            }
        }
        return string.Join(";", parts);
    }
}
