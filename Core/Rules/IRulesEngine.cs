using Core.Models;

namespace Core.Rules;

public interface IRulesEngine
{
    IReadOnlyList<NormalizedEvent> Evaluate(GameSnapshot snapshot);
    void Reset();
}
