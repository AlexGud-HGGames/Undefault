using Core.Models;

namespace Core.Diff;

public sealed record SnapshotDiff(
    GameSnapshot? Previous,
    GameSnapshot Current,
    PlayerDiff Player
)
{
    public bool IsFirstSnapshot => Previous is null;
}
