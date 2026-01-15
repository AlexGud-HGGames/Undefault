using System.Linq;

namespace Core.Models;

public sealed record GameSnapshot(
    DateTimeOffset Timestamp,
    string? GameId,
    string? MatchId,
    string? PlayerId,
    IReadOnlyList<ISnapshotModule> Modules
)
{
    public TModule? GetModule<TModule>() where TModule : class, ISnapshotModule
    {
        return Modules.OfType<TModule>().FirstOrDefault();
    }
}
