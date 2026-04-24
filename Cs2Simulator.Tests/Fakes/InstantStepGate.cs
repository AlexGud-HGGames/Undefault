using System.Threading;
using System.Threading.Tasks;
using Cs2Simulator.Runtime;

namespace Cs2Simulator.Tests.Fakes;

internal sealed class InstantStepGate : IStepGate
{
    public int Waits { get; private set; }
    public Task WaitAsync(CancellationToken ct)
    {
        Waits++;
        return Task.CompletedTask;
    }
}
