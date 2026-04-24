using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Cs2Simulator.Scenarios.State;
using Cs2Simulator.Scenarios.Ticks;

namespace Cs2Simulator.Scenarios.Scenarios;

public interface IScenario
{
    string Id { get; }
    string Name { get; }
    string Description { get; }

    /// <summary>
    /// Implementations MUST be C# async iterators (use <c>yield return</c>) and
    /// MUST annotate their <paramref name="ct"/> with
    /// <see cref="EnumeratorCancellationAttribute"/> so callers' tokens flow
    /// into the generator. The attribute is only honored on the implementation;
    /// it is a documented expectation here, not enforced by the compiler.
    /// </summary>
#pragma warning disable CS8424
    IAsyncEnumerable<SimulatedTick> Run(
        SimulationState state,
        [EnumeratorCancellation] CancellationToken ct);
#pragma warning restore CS8424
}
