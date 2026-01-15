namespace Core.Models;

public sealed record PositionModule(
    Vector3 Position,
    bool IsMoving
) : ISnapshotModule;
