namespace Core.Music;

/// <summary>
/// Authoritative gameplay-safety posture for music. See docs/music-safety-state-spec.md.
/// </summary>
public enum MusicSafetyState
{
    /// <summary>Cannot prove safe audibility; conservative defaults apply.</summary>
    Unknown = 0,

    /// <summary>Adaptive music may run under policy.</summary>
    Safe = 1,

    /// <summary>Suppress or hold danger floor; dominates mixer and envelopes.</summary>
    Danger = 2,
}
