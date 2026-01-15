namespace Core.Models;

public sealed record VitalsModule(
    int Health,
    int Armor,
    bool IsAlive
) : ISnapshotModule;
