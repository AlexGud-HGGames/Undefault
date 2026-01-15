using Core.Models;
using Microsoft.Extensions.Logging;

namespace Core.Actions;

public sealed class LogEventAction : IEventAction
{
    private readonly ILogger<LogEventAction> _logger;

    public LogEventAction(ILogger<LogEventAction> logger)
    {
        _logger = logger;
    }

    public string Key => "log";

    public void Execute(NormalizedEvent normalizedEvent)
    {
        _logger.LogInformation(
            "Event {EventType} at {Timestamp} (GameId={GameId}, MatchId={MatchId}, PlayerId={PlayerId})",
            normalizedEvent.Type,
            normalizedEvent.Timestamp,
            normalizedEvent.Context.GameId,
            normalizedEvent.Context.MatchId,
            normalizedEvent.Context.PlayerId
        );
    }
}
