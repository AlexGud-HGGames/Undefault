using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cs2Simulator.Cli;
using Cs2Simulator.Runtime;
using Cs2Simulator.Scenarios.Discovery;
using Cs2Simulator.Scenarios.Scenarios;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cs2Simulator;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        CliArgs cliArgs;
        try
        {
            cliArgs = CliArgs.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(CliArgs.HelpText);
            return 64;
        }

        if (cliArgs.ShowHelp)
        {
            Console.WriteLine(CliArgs.HelpText);
            return 0;
        }

        using var host = BuildHost(cliArgs);

        await host.StartAsync();

        var simulatorOptions = host.Services.GetRequiredService<SimulatorOptions>();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var cli = host.Services.GetRequiredService<SimulatorCli>();
        cli.Options.ResetBeforeRun = cliArgs.Reset ?? simulatorOptions.ResetBeforeRun;
        cli.Options.StepMode = cliArgs.Step;
        cli.Options.Speed = Speed.Parse(cliArgs.Speed ?? simulatorOptions.DefaultSpeed);

        int exitCode;
        try
        {
            if (!string.IsNullOrWhiteSpace(cliArgs.Scenario))
            {
                exitCode = await cli.RunOnceAsync(cliArgs.Scenario!, cts.Token);
            }
            else
            {
                exitCode = await cli.RunInteractiveAsync(cts.Token);
            }
        }
        finally
        {
            await host.StopAsync(TimeSpan.FromSeconds(2));
        }

        return exitCode;
    }

    private static IHost BuildHost(CliArgs cliArgs)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        builder.Configuration.AddEnvironmentVariables(prefix: "CS2SIM_");

        var simulatorSection = builder.Configuration.GetSection("Simulator");
        var simulatorOptions = simulatorSection.Get<SimulatorOptions>() ?? new SimulatorOptions();
        if (!string.IsNullOrWhiteSpace(cliArgs.Endpoint))
        {
            simulatorOptions.Endpoint = cliArgs.Endpoint!;
        }

        builder.Services.AddSingleton(simulatorOptions);
        builder.Services.AddSingleton(_ => ScenarioCatalog.Discover(typeof(IScenario).Assembly));
        builder.Services.AddSingleton<IStepGate, ConsoleStepGate>();
        builder.Services.AddSingleton<ScenarioRunner>();
        builder.Services.AddSingleton<SimulatorCli>();

        builder.Services.AddHttpClient<IGsiTransport, HttpGsiTransport>(client =>
        {
            client.BaseAddress = new Uri(NormalizeBase(simulatorOptions.Endpoint));
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return builder.Build();
    }

    private static string NormalizeBase(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return "http://127.0.0.1:5292/";
        }

        var trimmed = endpoint.Trim();
        return trimmed.EndsWith("/") ? trimmed : trimmed + "/";
    }
}
