using System.Text.Json;
using Core.Models;
using GsiHost.Configuration;
using GsiHost.Models;
using Microsoft.Extensions.Options;

namespace GsiHost.Services;

public sealed class TimelineCaptureService : IDisposable
{
    private const int MaxRecentEventKeys = 20;

    private readonly object _lock = new();
    private readonly GsiProcessingService _processor;
    private readonly TimelineOptions _options;
    private readonly RuntimeOptions _runtime;
    private readonly ILogger<TimelineCaptureService> _logger;
    private readonly List<TimelineEntry> _entries = new();
    private readonly Queue<string> _recentEventKeys = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private long _nextSequence;
    private string _sessionFilePath;
    private TimelineGameContext _latestContext = TimelineGameContext.Empty;

    public TimelineCaptureService(
        GsiProcessingService processor,
        IWebHostEnvironment environment,
        IOptions<TimelineOptions> options,
        IOptions<RuntimeOptions> runtime,
        ILogger<TimelineCaptureService> logger)
    {
        _processor = processor;
        _options = Normalize(options.Value);
        _runtime = runtime.Value;
        _logger = logger;
        _sessionFilePath = CreateSessionFilePath(environment.ContentRootPath, _options.Directory);

        if (_options.IsEnabled(_runtime))
        {
            _processor.Processed += OnProcessed;
        }
    }

    public IReadOnlyList<TimelineEntry> GetRecentEntries()
    {
        lock (_lock)
        {
            return _entries.ToList();
        }
    }

    public IReadOnlyList<IntentEpisode> GetIntentEpisodes()
    {
        lock (_lock)
        {
            var episodes = new List<IntentEpisode>();
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!string.Equals(entry.Source, TimelineSources.UserAction, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var beforeStart = Math.Max(0, i - _options.EpisodeBeforeEntryCount);
                var before = _entries
                    .Skip(beforeStart)
                    .Take(i - beforeStart)
                    .ToList();
                var after = _entries
                    .Skip(i + 1)
                    .Take(_options.EpisodeAfterEntryCount)
                    .ToList();

                episodes.Add(new IntentEpisode(entry, before, after));
            }

            return episodes;
        }
    }

    public TimelineEntry RecordUserAction(
        string eventKey,
        string? action,
        string? detail,
        TimelineCommandOutcome outcome,
        DateTimeOffset? timestampUtc = null)
    {
        return AppendEntry(
            timestampUtc ?? DateTimeOffset.UtcNow,
            TimelineSources.UserAction,
            EventKeys.Normalize(eventKey),
            action,
            detail,
            _latestContext,
            outcome);
    }

    public void Reset()
    {
        lock (_lock)
        {
            _entries.Clear();
            _recentEventKeys.Clear();
            _latestContext = TimelineGameContext.Empty;
            _nextSequence = 0;
            _sessionFilePath = CreateSiblingSessionFilePath(_sessionFilePath);
        }
    }

    private void OnProcessed(object? sender, GsiProcessedEventArgs args)
    {
        if (args.Snapshot is null)
        {
            return;
        }

        lock (_lock)
        {
            foreach (var normalizedEvent in args.Events)
            {
                AddRecentEventKey(normalizedEvent.EventKey);
            }

            _latestContext = TimelineGameContext.FromSnapshot(args.Snapshot, _recentEventKeys.ToList());

            foreach (var normalizedEvent in args.Events)
            {
                AppendEntryLocked(
                    normalizedEvent.Timestamp,
                    TimelineSources.Gsi,
                    normalizedEvent.EventKey,
                    action: null,
                    normalizedEvent.Detail,
                    _latestContext,
                    outcome: null);
            }
        }
    }

    private TimelineEntry AppendEntry(
        DateTimeOffset timestampUtc,
        string source,
        string eventKey,
        string? action,
        string? detail,
        TimelineGameContext context,
        TimelineCommandOutcome? outcome)
    {
        lock (_lock)
        {
            return AppendEntryLocked(timestampUtc, source, eventKey, action, detail, context, outcome);
        }
    }

    private TimelineEntry AppendEntryLocked(
        DateTimeOffset timestampUtc,
        string source,
        string eventKey,
        string? action,
        string? detail,
        TimelineGameContext context,
        TimelineCommandOutcome? outcome)
    {
        var entry = new TimelineEntry(
            Sequence: ++_nextSequence,
            TimestampUtc: timestampUtc,
            Source: source,
            EventKey: EventKeys.Normalize(eventKey),
            Action: string.IsNullOrWhiteSpace(action) ? null : action.Trim().ToLowerInvariant(),
            Detail: string.IsNullOrWhiteSpace(detail) ? null : detail.Trim(),
            GameContext: context,
            Outcome: outcome);

        if (_options.IsEnabled(_runtime))
        {
            _entries.Add(entry);
            TrimRing();
            AppendJsonLineLocked(entry);
        }

        return entry;
    }

    private void AddRecentEventKey(string eventKey)
    {
        var normalized = EventKeys.Normalize(eventKey);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        _recentEventKeys.Enqueue(normalized);
        while (_recentEventKeys.Count > MaxRecentEventKeys)
        {
            _recentEventKeys.Dequeue();
        }
    }

    private void TrimRing()
    {
        while (_entries.Count > _options.MaxInMemoryEntries)
        {
            _entries.RemoveAt(0);
        }
    }

    private void AppendJsonLineLocked(TimelineEntry entry)
    {
        try
        {
            var directory = Path.GetDirectoryName(_sessionFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            var line = JsonSerializer.Serialize(entry, _jsonOptions);
            File.AppendAllText(_sessionFilePath, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to append timeline entry.");
        }
    }

    private static TimelineOptions Normalize(TimelineOptions options)
    {
        options.MaxInMemoryEntries = Math.Clamp(options.MaxInMemoryEntries, 1, 100_000);
        options.EpisodeBeforeEntryCount = Math.Clamp(options.EpisodeBeforeEntryCount, 0, 1_000);
        options.EpisodeAfterEntryCount = Math.Clamp(options.EpisodeAfterEntryCount, 0, 1_000);
        options.Directory = string.IsNullOrWhiteSpace(options.Directory)
            ? "timeline"
            : options.Directory.Trim();
        return options;
    }

    private static string CreateSessionFilePath(string contentRootPath, string configuredDirectory)
    {
        var directory = Path.IsPathRooted(configuredDirectory)
            ? configuredDirectory
            : Path.Combine(contentRootPath, configuredDirectory);

        return Path.Combine(directory, BuildSessionFileName());
    }

    private static string CreateSiblingSessionFilePath(string currentFilePath)
    {
        var directory = Path.GetDirectoryName(currentFilePath) ?? AppContext.BaseDirectory;
        return Path.Combine(directory, BuildSessionFileName());
    }

    private static string BuildSessionFileName()
    {
        return $"session-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.jsonl";
    }

    public void Dispose()
    {
        if (_options.IsEnabled(_runtime))
        {
            _processor.Processed -= OnProcessed;
        }
    }
}
