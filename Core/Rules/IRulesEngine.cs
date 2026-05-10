using System.Threading;
using System.Threading.Tasks;
using Core.Adapters;
using Core.Models;

namespace Core.Rules;

public interface IRulesEngine
{
    Task<IReadOnlyList<NormalizedEvent>> EvaluateAsync(
        AdapterObservation observation,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NormalizedEvent>> DetectAsync(
        AdapterObservation observation,
        CancellationToken cancellationToken = default);

    void Reset();
}
