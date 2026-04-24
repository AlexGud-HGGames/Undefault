using System;
using System.Threading;
using System.Threading.Tasks;
using Cs2Simulator.Runtime;

namespace Cs2Simulator.Cli;

public sealed class ConsoleStepGate : IStepGate
{
    public async Task WaitAsync(CancellationToken ct)
    {
        Console.Write("[step] press ENTER for next tick (Ctrl+C to stop) ");
        while (!ct.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar)
                {
                    Console.WriteLine();
                    return;
                }
            }

            try
            {
                await Task.Delay(50, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }

        ct.ThrowIfCancellationRequested();
    }
}
