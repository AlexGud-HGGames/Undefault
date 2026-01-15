using Core.Models;

namespace Core.Stores;

public interface ISnapshotStore
{
    GameSnapshot? GetLast();
    void Save(GameSnapshot snapshot);
    void Clear();
}
