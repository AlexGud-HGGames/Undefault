using Core.Actions;
using Core.Diff;
using Core.Models;
using Core.Stores;
using Microsoft.Extensions.Options;

namespace Core.Rules;

public sealed class RulesEngine : IRulesEngine
{
    private readonly ISnapshotStore _snapshotStore;
    private readonly SnapshotDiffer _differ;
    private readonly EventDetector _detector;
    private readonly IReadOnlyDictionary<EventType, IReadOnlyList<string>> _actionMap;
    private readonly IReadOnlyDictionary<string, IEventAction> _actionsByKey;

    public RulesEngine(
        ISnapshotStore snapshotStore,
        SnapshotDiffer differ,
        EventDetector detector,
        IEnumerable<IEventAction> actions,
        IOptions<RulesEngineOptions>? options = null)
    {
        _snapshotStore = snapshotStore;
        _differ = differ;
        _detector = detector;

        _actionsByKey = actions
            .GroupBy(action => action.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        _actionMap = (options?.Value?.ActionMap ?? new Dictionary<EventType, List<string>>())
            .ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value,
                EqualityComparer<EventType>.Default
            );
    }

    public IReadOnlyList<NormalizedEvent> Evaluate(GameSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var previous = _snapshotStore.GetLast();
        var diff = _differ.Compute(previous, snapshot);
        var events = _detector.Detect(diff);
        _snapshotStore.Save(snapshot);

        foreach (var normalizedEvent in events)
        {
            if (!_actionMap.TryGetValue(normalizedEvent.Type, out var actionKeys))
            {
                continue;
            }

            foreach (var actionKey in actionKeys)
            {
                if (_actionsByKey.TryGetValue(actionKey, out var action))
                {
                    action.Execute(normalizedEvent);
                }
            }
        }

        return events;
    }

    public void Reset()
    {
        _detector.Reset();
        _snapshotStore.Clear();
    }
}
