using Core.Adapters;
using Core.Diff;

namespace Core.Rules;

/// <summary>
/// Per-tick input to <see cref="EventDetector"/>. The detector consumes only neutral
/// signals from <see cref="AdapterObservation"/> for round/death decisions; <see cref="Activity"/>
/// remains available for non-baseline branches (combat / idle) that still rely on raw module diffs.
/// </summary>
public sealed record NeutralDetectorContext(
    AdapterObservation Current,
    AdapterObservation? Previous,
    ActivityDiff Activity,
    bool IsFirstObservation);
