using Core.Music;

namespace Core.Adapters;

public sealed record SafetyFacts(
    MusicSafetyState State,
    string? Reason,
    bool IsStale)
{
    public static SafetyFacts Unknown(string? reason = null)
    {
        return new SafetyFacts(
            State: MusicSafetyState.Unknown,
            Reason: reason,
            IsStale: false);
    }
}
