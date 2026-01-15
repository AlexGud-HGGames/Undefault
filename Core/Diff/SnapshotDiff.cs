using Core.Models;

namespace Core.Diff;

public sealed record SnapshotDiff(
    GameSnapshot? Previous,
    GameSnapshot Current,
    ActivityDiff Activity
)
{
    public bool IsFirstSnapshot => Previous is null;
}
