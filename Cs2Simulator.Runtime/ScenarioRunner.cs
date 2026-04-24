using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Cs2Simulator.Scenarios.Scenarios;
using Cs2Simulator.Scenarios.State;
using Microsoft.Extensions.Logging;

namespace Cs2Simulator.Runtime;

public sealed class ScenarioRunner
{
    private readonly IGsiTransport _transport;
    private readonly IStepGate _stepGate;
    private readonly ILogger<ScenarioRunner> _logger;

    public ScenarioRunner(
        IGsiTransport transport,
        IStepGate stepGate,
        ILogger<ScenarioRunner> logger)
    {
        _transport = transport;
        _stepGate = stepGate;
        _logger = logger;
    }

    public async Task RunAsync(
        IScenario scenario,
        ScenarioRunOptions options,
        CancellationToken ct)
    {
        if (scenario is null) throw new ArgumentNullException(nameof(scenario));
        options ??= new ScenarioRunOptions();

        _logger.LogInformation(
            "Scenario starting: {Id} ({Name}) speed={Speed} step={Step} reset={Reset}",
            scenario.Id, scenario.Name, options.Speed, options.StepMode, options.ResetBeforeRun);

        if (options.ResetBeforeRun)
        {
            await _transport.ResetAsync(ct).ConfigureAwait(false);
            LogDetail(options, "Reset host state via /gsi/reset.");
        }

        var state = new SimulationState();
        var startedAt = state.Clock.Epoch;
        var tickIndex = 0;

        await foreach (var tick in scenario.Run(state, ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();

            var wallDelay = options.Speed.Scale(tick.Delay);
            if (wallDelay > TimeSpan.Zero)
            {
                await Task.Delay(wallDelay, ct).ConfigureAwait(false);
            }
            else
            {
                await Task.Yield();
            }

            if (options.StepMode)
            {
                await _stepGate.WaitAsync(ct).ConfigureAwait(false);
            }

            var simulatedOffset = state.Clock.Now - startedAt;
            var expected = tick.ExpectedEventKey is null
                ? string.Empty
                : $" (expected: {tick.ExpectedEventKey})";
            LogTick(
                options,
                "[t+{Offset:0.00}s] tick #{Index}: {Description}{Expected}",
                simulatedOffset.TotalSeconds, tickIndex, tick.Description, expected);

            await _transport.SendAsync(tick.Payload, ct).ConfigureAwait(false);

            LogTick(
                options,
                "  state: round={Round} map.phase={MapPhase} round.phase={RoundPhase} bomb={Bomb} hp={Hp}",
                state.MapRound, state.MapPhase, state.RoundPhase,
                state.BombState ?? "-",
                state.PlayerHealth.ToString(CultureInfo.InvariantCulture));

            tickIndex++;
        }

        _logger.LogInformation("Scenario finished: {Id} ({Count} ticks)", scenario.Id, tickIndex);
    }

    private void LogDetail(ScenarioRunOptions options, string message)
    {
        if (options.VerboseLogging)
        {
            _logger.LogInformation(message);
        }
        else
        {
            _logger.LogDebug(message);
        }
    }

    private void LogTick(ScenarioRunOptions options, string messageTemplate, params object?[] args)
    {
        if (options.VerboseLogging)
        {
            _logger.LogInformation(messageTemplate, args);
        }
        else
        {
            _logger.LogDebug(messageTemplate, args);
        }
    }
}
