using Core.Models;
using Core.Stores;

namespace GsiHost.Services;

public sealed class InMemorySnapshotStore : ISnapshotStore
{
    private readonly object _lock = new();
    private GameSnapshot? _last;

    public GameSnapshot? GetLast()
    {
        lock (_lock)
        {
            return _last;
        }
    }

    public void Save(GameSnapshot snapshot)
    {
        lock (_lock)
        {
            _last = snapshot;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _last = null;
        }
    }
}
