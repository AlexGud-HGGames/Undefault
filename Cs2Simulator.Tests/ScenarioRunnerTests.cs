using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Cs2Simulator.Runtime;
using Cs2Simulator.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cs2Simulator.Tests;

public sealed class ScenarioRunnerTests
{
    private static ScenarioRunner Build(
        out FakeGsiTransport transport,
        out InstantStepGate gate)
    {
        transport = new FakeGsiTransport();
        gate = new InstantStepGate();
        return new ScenarioRunner(transport, gate, NullLogger<ScenarioRunner>.Instance);
    }

    [Fact]
    public async Task RunAsync_ResetBeforeRun_CallsResetOnce_ThenSendsAllTicks()
    {
        var runner = Build(out var transport, out _);
        var scenario = new TwoTickScenario { LiveDelay = TimeSpan.Zero };

        await runner.RunAsync(scenario, new ScenarioRunOptions { ResetBeforeRun = true }, CancellationToken.None);

        transport.ResetCalls.Should().Be(1);
        transport.Sends.Should().HaveCount(2);
    }

    [Fact]
    public async Task RunAsync_NoReset_DoesNotCallReset()
    {
        var runner = Build(out var transport, out _);

        await runner.RunAsync(new TwoTickScenario { LiveDelay = TimeSpan.Zero },
            new ScenarioRunOptions { ResetBeforeRun = false },
            CancellationToken.None);

        transport.ResetCalls.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_RestartCallsResetAgain()
    {
        var runner = Build(out var transport, out _);
        var options = new ScenarioRunOptions { ResetBeforeRun = true };

        await runner.RunAsync(new TwoTickScenario { LiveDelay = TimeSpan.Zero }, options, CancellationToken.None);
        await runner.RunAsync(new TwoTickScenario { LiveDelay = TimeSpan.Zero }, options, CancellationToken.None);

        transport.ResetCalls.Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_StepMode_UsesStepGateBetweenTicks()
    {
        var runner = Build(out _, out var gate);

        await runner.RunAsync(new TwoTickScenario { LiveDelay = TimeSpan.Zero },
            new ScenarioRunOptions { StepMode = true, ResetBeforeRun = false },
            CancellationToken.None);

        gate.Waits.Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_NoStepMode_DoesNotUseGate()
    {
        var runner = Build(out _, out var gate);

        await runner.RunAsync(new TwoTickScenario { LiveDelay = TimeSpan.Zero },
            new ScenarioRunOptions { StepMode = false, ResetBeforeRun = false },
            CancellationToken.None);

        gate.Waits.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_SpeedMultiplier_ScalesWallClock_ButNotSimulatedTimestamps()
    {
        var runner1x = Build(out var transport1x, out _);
        var runner5x = Build(out var transport5x, out _);
        var liveDelay = TimeSpan.FromSeconds(2);
        var sw1 = Stopwatch.StartNew();
        await runner1x.RunAsync(new TwoTickScenario { LiveDelay = liveDelay },
            new ScenarioRunOptions { ResetBeforeRun = false, Speed = Speed.Normal },
            CancellationToken.None);
        sw1.Stop();

        var sw5 = Stopwatch.StartNew();
        await runner5x.RunAsync(new TwoTickScenario { LiveDelay = liveDelay },
            new ScenarioRunOptions { ResetBeforeRun = false, Speed = new Speed(5) },
            CancellationToken.None);
        sw5.Stop();

        sw1.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(1500);
        sw5.ElapsedMilliseconds.Should().BeLessThan(sw1.ElapsedMilliseconds);

        var ts1 = transport1x.Sends[1].Provider!.Timestamp!.Value
                  - transport1x.Sends[0].Provider!.Timestamp!.Value;
        var ts5 = transport5x.Sends[1].Provider!.Timestamp!.Value
                  - transport5x.Sends[0].Provider!.Timestamp!.Value;
        ts1.Should().Be(2L);
        ts5.Should().Be(2L);
    }

    [Fact]
    public async Task RunAsync_MaxSpeed_FinishesQuickly_AndStaysCancellable()
    {
        var runner = Build(out var transport, out _);
        var scenario = new TwoTickScenario { LiveDelay = TimeSpan.FromSeconds(10) };

        var sw = Stopwatch.StartNew();
        await runner.RunAsync(scenario,
            new ScenarioRunOptions { ResetBeforeRun = false, Speed = Speed.Max },
            CancellationToken.None);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(500);
        transport.Sends.Should().HaveCount(2);
    }

    [Fact]
    public async Task RunAsync_Cancellation_StopsPromptly()
    {
        var runner = Build(out var transport, out _);
        using var cts = new CancellationTokenSource();

        var task = runner.RunAsync(new HangingScenario(),
            new ScenarioRunOptions { ResetBeforeRun = false, Speed = Speed.Normal },
            cts.Token);

        await Task.Delay(100);
        cts.Cancel();

        var act = async () => await task;
        await act.Should().ThrowAsync<OperationCanceledException>();
        transport.Sends.Count.Should().BeGreaterThan(0);
    }
}
