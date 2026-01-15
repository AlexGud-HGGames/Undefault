using Core.Models;
using Microsoft.Extensions.Logging;

namespace Core.Actions;

public abstract class AsyncEventActionBase : IEventAction
{
    protected readonly ILogger Logger;

    protected AsyncEventActionBase(ILogger logger)
    {
        Logger = logger;
    }

    public abstract string Key { get; }

    public void Execute(NormalizedEvent normalizedEvent)
    {
        Task.Run(async () =>
        {
            try
            {
                await ExecuteAsync(normalizedEvent);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error executing async action {ActionKey} for event {EventType}", Key, normalizedEvent.Type);
            }
        });
    }

    protected abstract Task ExecuteAsync(NormalizedEvent normalizedEvent);
}
