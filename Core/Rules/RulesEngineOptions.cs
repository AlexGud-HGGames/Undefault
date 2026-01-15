using Core.Models;

namespace Core.Rules;

public sealed class RulesEngineOptions
{
    public Dictionary<EventType, List<string>> ActionMap { get; init; } = new();
}
