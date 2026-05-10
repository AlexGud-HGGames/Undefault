using System.Threading;
using System.Threading.Tasks;
using Core.Actions;
using Core.Adapters;
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
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _actionMap;
    private readonly IReadOnlyDictionary<string, IEventAction> _actionsByKey;
    private AdapterObservation? _previousObservation;

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

        _actionMap = (options?.Value?.ActionMap ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase))
            .ToDictionary(
                pair => EventKeys.Normalize(pair.Key),
                pair => (IReadOnlyList<string>)pair.Value,
                StringComparer.OrdinalIgnoreCase
            );
    }

    public async Task<IReadOnlyList<NormalizedEvent>> EvaluateAsync(
        AdapterObservation observation,
        CancellationToken cancellationToken = default)
    {
        var events = Detect(observation);
        await ExecuteActionsAsync(events, cancellationToken).ConfigureAwait(false);
        return events;
    }

    public Task<IReadOnlyList<NormalizedEvent>> DetectAsync(
        AdapterObservation observation,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Detect(observation));
    }

    private IReadOnlyList<NormalizedEvent> Detect(AdapterObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        var previousRaw = _snapshotStore.GetLast();
        var diff = _differ.Compute(previousRaw, observation.Raw);

        var context = new NeutralDetectorContext(
            Current: observation,
            Previous: _previousObservation,
            Activity: diff.Activity,
            IsFirstObservation: _previousObservation is null);

        var events = _detector.Detect(context);

        _snapshotStore.Save(observation.Raw);
        _previousObservation = observation;

        return events;
    }

    private async Task ExecuteActionsAsync(
        IReadOnlyList<NormalizedEvent> events,
        CancellationToken cancellationToken)
    {
        foreach (var normalizedEvent in events)
        {
            if (!_actionMap.TryGetValue(normalizedEvent.EventKey, out var actionKeys))
            {
                continue;
            }

            foreach (var actionKey in actionKeys)
            {
                if (_actionsByKey.TryGetValue(actionKey, out var action))
                {
                    await action.ExecuteAsync(normalizedEvent, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    public void Reset()
    {
        _detector.Reset();
        _snapshotStore.Clear();
        _previousObservation = null;
    }
}
