using System.Collections.Generic;
using Core.Music;

namespace GsiHost.Services;

public sealed class InMemoryShadowMusicSnapshotSink : IShadowMusicSnapshotSink
{
    public const int DefaultCapacity = 32;

    private readonly object _gate = new();
    private readonly LinkedList<MusicEngineDebugSnapshot> _entries = new();
    private readonly int _capacity;

    public InMemoryShadowMusicSnapshotSink()
        : this(DefaultCapacity)
    {
    }

    public InMemoryShadowMusicSnapshotSink(int capacity)
    {
        _capacity = capacity > 0 ? capacity : DefaultCapacity;
    }

    public MusicEngineDebugSnapshot? Latest
    {
        get
        {
            lock (_gate)
            {
                return _entries.Last?.Value;
            }
        }
    }

    public void Record(MusicEngineDebugSnapshot snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        lock (_gate)
        {
            _entries.AddLast(snapshot);
            while (_entries.Count > _capacity)
            {
                _entries.RemoveFirst();
            }
        }
    }

    public IReadOnlyList<MusicEngineDebugSnapshot> Recent()
    {
        lock (_gate)
        {
            return _entries.Count == 0
                ? Array.Empty<MusicEngineDebugSnapshot>()
                : _entries.ToArray();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }
}
