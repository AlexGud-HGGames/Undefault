using System;
using System.Threading;
using System.Threading.Tasks;
using Cs2Simulator.Runtime;
using Cs2Simulator.Scenarios.Discovery;
using Cs2Simulator.Scenarios.Scenarios;
using Microsoft.Extensions.Logging;

namespace Cs2Simulator.Cli;

public sealed class SimulatorCli
{
    private readonly ScenarioCatalog _catalog;
    private readonly ScenarioRunner _runner;
    private readonly IGsiTransport _transport;
    private readonly ILogger<SimulatorCli> _logger;

    private IScenario? _currentScenario;
    private readonly ScenarioRunOptions _runOptions = new();

    public SimulatorCli(
        ScenarioCatalog catalog,
        ScenarioRunner runner,
        IGsiTransport transport,
        SimulatorOptions simulatorOptions,
        ILogger<SimulatorCli> logger)
    {
        _catalog = catalog;
        _runner = runner;
        _transport = transport;
        _logger = logger;
        _runOptions.VerboseLogging = simulatorOptions.LogVerbose;
    }

    public async Task<int> RunInteractiveAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("CS2 GSI Simulator");
        Console.WriteLine("Type a number then ENTER. 'q' to quit. 'h' for help.");
        Console.WriteLine("While a scenario runs, press Ctrl+C to cancel.");
        ListScenarios();

        while (!ct.IsCancellationRequested)
        {
            Console.WriteLine();
            Console.WriteLine($"current: {(_currentScenario?.Id ?? "<none>")} | speed={_runOptions.Speed} | step={(_runOptions.StepMode ? "on" : "off")} | reset={(_runOptions.ResetBeforeRun ? "on" : "off")}");
            Console.Write("[1]list [2]run [3]restart [4]speed [5]step [6]reset host [q]uit > ");
            var line = Console.ReadLine();
            if (line is null) break;
            line = line.Trim();
            if (line.Length == 0) continue;

            try
            {
                switch (line)
                {
                    case "1":
                    case "list":
                        ListScenarios();
                        break;
                    case "2":
                    case "run":
                        await RunPickAsync(ct);
                        break;
                    case "3":
                    case "restart":
                        await RestartAsync(ct);
                        break;
                    case "4":
                    case "speed":
                        ChangeSpeed();
                        break;
                    case "5":
                    case "step":
                        _runOptions.StepMode = !_runOptions.StepMode;
                        Console.WriteLine($"step mode = {(_runOptions.StepMode ? "on" : "off")}");
                        break;
                    case "6":
                    case "reset":
                        await _transport.ResetAsync(ct);
                        Console.WriteLine("host state reset.");
                        break;
                    case "h":
                    case "help":
                        Console.WriteLine(CliArgs.HelpText);
                        break;
                    case "q":
                    case "quit":
                    case "exit":
                        Console.WriteLine("bye");
                        return 0;
                    default:
                        Console.WriteLine($"unknown command: {line}");
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("(cancelled)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Command failed");
            }
        }

        return 0;
    }

    public async Task<int> RunOnceAsync(string scenarioId, CancellationToken ct)
    {
        if (!_catalog.TryGet(scenarioId, out var scenario))
        {
            Console.Error.WriteLine($"Unknown scenario id: {scenarioId}");
            ListScenarios();
            return 2;
        }

        try
        {
            await _runner.RunAsync(scenario, _runOptions, ct);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 130;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scenario {Id} failed", scenarioId);
            return 1;
        }
    }

    public ScenarioRunOptions Options => _runOptions;

    private void ListScenarios()
    {
        Console.WriteLine();
        Console.WriteLine("scenarios:");
        foreach (var scenario in _catalog.All)
        {
            Console.WriteLine($"  {scenario.Id,-22} {scenario.Name}");
            Console.WriteLine($"  {string.Empty,-22} {scenario.Description}");
        }
    }

    private async Task RunPickAsync(CancellationToken ct)
    {
        Console.Write("scenario id (blank cancels): ");
        var id = (Console.ReadLine() ?? string.Empty).Trim();
        if (id.Length == 0)
        {
            return;
        }

        if (!_catalog.TryGet(id, out var scenario))
        {
            Console.WriteLine($"unknown scenario id: {id}");
            return;
        }

        _currentScenario = scenario;
        await _runner.RunAsync(scenario, _runOptions, ct);
    }

    private async Task RestartAsync(CancellationToken ct)
    {
        if (_currentScenario is null)
        {
            Console.WriteLine("no scenario chosen yet (use 'run' first).");
            return;
        }

        await _runner.RunAsync(_currentScenario, _runOptions, ct);
    }

    private void ChangeSpeed()
    {
        Console.Write("speed (1|2|5|max): ");
        var raw = Console.ReadLine();
        try
        {
            _runOptions.Speed = Speed.Parse(raw);
            Console.WriteLine($"speed = {_runOptions.Speed}");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}
