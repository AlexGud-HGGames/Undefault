using System;

namespace Cs2Simulator.Scenarios.State;

public sealed class SimulatedClock
{
    private DateTimeOffset _now;

    public SimulatedClock()
        : this(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000))
    {
    }

    public SimulatedClock(DateTimeOffset epoch)
    {
        _now = epoch;
        Epoch = epoch;
    }

    public DateTimeOffset Epoch { get; }

    public DateTimeOffset Now => _now;

    /// <summary>
    /// Whole Unix seconds for <c>provider.timestamp</c>. Sub-second advances from
    /// <see cref="Advance"/> collapse on the wire until the second boundary crosses.
    /// </summary>
    public long UnixSeconds => _now.ToUnixTimeSeconds();

    public TimeSpan Elapsed => _now - Epoch;

    public void Advance(TimeSpan delta)
    {
        if (delta < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "Simulated clock cannot move backwards.");
        }

        _now = _now.Add(delta);
    }
}
