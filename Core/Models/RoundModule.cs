namespace Core.Models;

public sealed record RoundModule(
    int? Round,
    string? Phase
) : ISnapshotModule;
