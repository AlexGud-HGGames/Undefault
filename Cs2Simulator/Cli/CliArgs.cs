using System;
using System.Collections.Generic;

namespace Cs2Simulator.Cli;

public sealed class CliArgs
{
    public string? Scenario { get; init; }
    public string? Speed { get; init; }
    public bool Step { get; init; }
    public bool Once { get; init; }
    public bool? Reset { get; init; }
    public string? Endpoint { get; init; }
    public bool ShowHelp { get; init; }

    public static CliArgs Parse(IReadOnlyList<string> args)
    {
        string? scenario = null;
        string? speed = null;
        var step = false;
        var once = false;
        bool? reset = null;
        string? endpoint = null;
        var help = false;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--scenario":
                case "-s":
                    scenario = NextValue(args, ref i, arg);
                    break;
                case "--speed":
                    speed = NextValue(args, ref i, arg);
                    break;
                case "--step":
                    step = true;
                    break;
                case "--once":
                    once = true;
                    break;
                case "--reset":
                    reset = true;
                    break;
                case "--no-reset":
                    reset = false;
                    break;
                case "--endpoint":
                case "-e":
                    endpoint = NextValue(args, ref i, arg);
                    break;
                case "--help":
                case "-h":
                case "/?":
                    help = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }
        }

        return new CliArgs
        {
            Scenario = scenario,
            Speed = speed,
            Step = step,
            Once = once,
            Reset = reset,
            Endpoint = endpoint,
            ShowHelp = help
        };
    }

    public const string HelpText = """
        UndefaultIt CS2 GSI Simulator

        Usage:
          dotnet run --project Cs2Simulator [options]

        Options:
          --scenario, -s <id>      Run this scenario once, then exit (exit codes below)
          --speed <1|2|5|max>      Wall-clock speed multiplier (default 1)
          --step                   Pause between ticks (press ENTER to advance)
          --once                   Optional; kept for scripts. --scenario already implies a single run.
          --reset / --no-reset     Toggle POST /gsi/reset before each run (default on)
          --endpoint, -e <url>     GsiHost base URL (default from appsettings.json)
          --help, -h               Show this help

        With no --scenario, an interactive menu lists scenarios and runs them on demand.

        Exit codes (non-interactive / --scenario):
          0 success, 1 scenario or transport error, 2 unknown scenario id,
          64 invalid CLI args, 130 cancelled (Ctrl+C)
        """;

    private static string NextValue(IReadOnlyList<string> args, ref int i, string flag)
    {
        if (i + 1 >= args.Count)
        {
            throw new ArgumentException($"Missing value for {flag}");
        }

        i++;
        return args[i];
    }
}
