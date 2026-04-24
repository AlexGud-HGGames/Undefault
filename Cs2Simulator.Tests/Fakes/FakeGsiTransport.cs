using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cs2Simulator.Runtime;
using Cs2Simulator.Scenarios.Models;

namespace Cs2Simulator.Tests.Fakes;

internal sealed class FakeGsiTransport : IGsiTransport
{
    public List<Cs2Payload> Sends { get; } = new();
    public int ResetCalls { get; private set; }

    public Task SendAsync(Cs2Payload payload, CancellationToken ct)
    {
        Sends.Add(payload);
        return Task.CompletedTask;
    }

    public Task ResetAsync(CancellationToken ct)
    {
        ResetCalls++;
        return Task.CompletedTask;
    }
}
