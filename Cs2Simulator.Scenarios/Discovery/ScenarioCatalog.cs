using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cs2Simulator.Scenarios.Scenarios;

namespace Cs2Simulator.Scenarios.Discovery;

/// <summary>
/// Reflective catalog of <see cref="IScenario"/> implementations.
/// Activation policy: every concrete scenario must expose a public
/// parameterless constructor. The Scenarios library is pure, so no DI.
/// </summary>
public sealed class ScenarioCatalog
{
    private readonly Dictionary<string, IScenario> _byId;

    private ScenarioCatalog(IReadOnlyList<IScenario> scenarios)
    {
        All = scenarios;
        _byId = scenarios.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IScenario> All { get; }

    public bool TryGet(string id, out IScenario scenario)
    {
        if (!string.IsNullOrWhiteSpace(id) && _byId.TryGetValue(id.Trim(), out var found))
        {
            scenario = found;
            return true;
        }

        scenario = null!;
        return false;
    }

    public static ScenarioCatalog Discover(Assembly assembly)
    {
        if (assembly is null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        var scenarioType = typeof(IScenario);

        var candidates = assembly
            .GetTypes()
            .Where(type =>
                scenarioType.IsAssignableFrom(type)
                && type is { IsClass: true, IsAbstract: false, IsInterface: false }
                && !type.ContainsGenericParameters)
            .ToList();

        var missingCtor = candidates
            .Where(type => type.GetConstructor(Type.EmptyTypes) is null)
            .Select(type => type.FullName ?? type.Name)
            .ToList();

        if (missingCtor.Count > 0)
        {
            throw new InvalidOperationException(
                "The following IScenario implementations lack a public parameterless constructor: "
                + string.Join(", ", missingCtor));
        }

        var instances = candidates
            .Select(type => (IScenario)Activator.CreateInstance(type)!)
            .ToList();

        var emptyMetadata = instances
            .Where(s => string.IsNullOrWhiteSpace(s.Id)
                || string.IsNullOrWhiteSpace(s.Name)
                || string.IsNullOrWhiteSpace(s.Description))
            .Select(s => s.GetType().FullName ?? s.GetType().Name)
            .ToList();

        if (emptyMetadata.Count > 0)
        {
            throw new InvalidOperationException(
                "The following IScenario implementations have an empty Id, Name, or Description: "
                + string.Join(", ", emptyMetadata));
        }

        var duplicates = instances
            .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                "Duplicate scenario IDs (case-insensitive): " + string.Join(", ", duplicates));
        }

        var ordered = instances
            .OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ScenarioCatalog(ordered);
    }
}
