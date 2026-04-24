using System.Threading;
using System.Threading.Tasks;
using Cs2Simulator.Scenarios.Models;

namespace Cs2Simulator.Runtime;

public interface IGsiTransport
{
    Task SendAsync(Cs2Payload payload, CancellationToken ct);
    Task ResetAsync(CancellationToken ct);
}
