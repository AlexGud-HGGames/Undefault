using Core.Models;

namespace Core.Actions;

public interface IEventAction
{
    string Key { get; }
    void Execute(NormalizedEvent normalizedEvent);
}
