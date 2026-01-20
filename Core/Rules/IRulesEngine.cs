using System.Threading;
using System.Threading.Tasks;
using Core.Models;

namespace Core.Rules;

public interface IRulesEngine
{
    Task<IReadOnlyList<NormalizedEvent>> EvaluateAsync(
        GameSnapshot snapshot,
        CancellationToken cancellationToken = default);
    void Reset();
}
