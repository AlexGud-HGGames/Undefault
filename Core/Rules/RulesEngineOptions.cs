using System;

namespace Core.Rules;

public sealed class RulesEngineOptions
{
    public Dictionary<string, List<string>> ActionMap { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}
