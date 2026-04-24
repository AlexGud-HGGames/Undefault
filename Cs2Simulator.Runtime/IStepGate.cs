using System.Threading;
using System.Threading.Tasks;

namespace Cs2Simulator.Runtime;

/// <summary>
/// Awaited between ticks when step-mode is on. The console app supplies an
/// implementation that reads ENTER; tests inject a programmable gate.
/// </summary>
public interface IStepGate
{
    Task WaitAsync(CancellationToken ct);
}
