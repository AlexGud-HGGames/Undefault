using Microsoft.Extensions.Logging;

namespace Core.Music;

/// <summary>
/// In-process <see cref="IMusicPlayer"/> for tests and <c>--quick</c>. Always available unless tests mark it otherwise.
/// </summary>
public sealed class MockMusicPlayer : IMusicPlayer
{
    private readonly ILogger<MockMusicPlayer> _logger;
    private readonly object _sync = new();
    private bool _available = true;
    private PlaybackStatus _status = PlaybackStatus.Stopped;
    private int _volume = 50;
    private MusicTrack? _track;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockMusicPlayer"/> class.
    /// </summary>
    /// <param name="logger">The logger used for mock playback diagnostics.</param>
    public MockMusicPlayer(ILogger<MockMusicPlayer> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public MusicPlayerCapabilities Capabilities => MusicPlayerCapabilities.Mvp;

    /// <summary>
    /// Gets or sets a value that indicates whether the mock reports itself as reachable.
    /// </summary>
    public bool Available
    {
        get
        {
            lock (_sync)
            {
                return _available;
            }
        }
        set
        {
            lock (_sync)
            {
                _available = value;
            }
        }
    }

    /// <summary>
    /// Gets the number of <see cref="PlayAsync"/> invocations.
    /// </summary>
    public int PlayCalls { get; private set; }

    /// <summary>
    /// Gets the number of <see cref="PauseAsync"/> invocations.
    /// </summary>
    public int PauseCalls { get; private set; }

    /// <summary>
    /// Gets the number of <see cref="ResumeAsync"/> invocations.
    /// </summary>
    public int ResumeCalls { get; private set; }

    /// <summary>
    /// Gets the number of <see cref="NextAsync"/> invocations.
    /// </summary>
    public int NextCalls { get; private set; }

    /// <summary>
    /// Gets the number of <see cref="PreviousAsync"/> invocations.
    /// </summary>
    public int PreviousCalls { get; private set; }

    /// <summary>
    /// Gets the volume values passed to <see cref="SetVolumeAsync"/>.
    /// </summary>
    public List<int> VolumeCalls { get; } = new();

    /// <summary>
    /// Gets the combined count of transport side effects used by host integration tests.
    /// </summary>
    public int PlaybackSideEffectCalls =>
        PlayCalls + PauseCalls + ResumeCalls + NextCalls + PreviousCalls + VolumeCalls.Count;

    /// <inheritdoc />
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Available);
    }

    /// <inheritdoc />
    public Task<MusicPlaybackState?> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (!_available)
            {
                return Task.FromResult<MusicPlaybackState?>(null);
            }

            return Task.FromResult<MusicPlaybackState?>(new MusicPlaybackState(_status, _track, _volume));
        }
    }

    /// <inheritdoc />
    public Task PlayAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            PlayCalls++;
            if (!_available)
            {
                return Task.CompletedTask;
            }

            _status = PlaybackStatus.Playing;
            _track ??= DefaultTrack;
        }

        _logger.LogInformation("[MOCK] Would play");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task PauseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            PauseCalls++;
            if (!_available)
            {
                return Task.CompletedTask;
            }

            if (_status == PlaybackStatus.Playing)
            {
                _status = PlaybackStatus.Paused;
            }
        }

        _logger.LogInformation("[MOCK] Would pause playback");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            ResumeCalls++;
            if (!_available)
            {
                return Task.CompletedTask;
            }

            if (_status != PlaybackStatus.Playing)
            {
                _status = PlaybackStatus.Playing;
                _track ??= DefaultTrack;
            }
        }

        _logger.LogInformation("[MOCK] Would resume playback");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task NextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            NextCalls++;
        }

        _logger.LogInformation("[MOCK] Would skip to next track");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task PreviousAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            PreviousCalls++;
        }

        _logger.LogInformation("[MOCK] Would skip to previous track");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetVolumeAsync(int volumePercent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (volumePercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(volumePercent), "Volume must be between 0 and 100.");
        }

        lock (_sync)
        {
            VolumeCalls.Add(volumePercent);
            if (_available)
            {
                _volume = volumePercent;
            }
        }

        _logger.LogInformation("[MOCK] Would set volume to {Volume}%", volumePercent);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Seeds playback status for tests without going through transport methods.
    /// </summary>
    /// <param name="status">One of the enumeration values that specifies the status to apply.</param>
    /// <param name="volumePercent">The volume to report, or <see langword="null"/> to keep the current volume.</param>
    /// <param name="track">The track to report, or <see langword="null"/> to keep the current track.</param>
    public void SeedState(PlaybackStatus status, int? volumePercent = null, MusicTrack? track = null)
    {
        lock (_sync)
        {
            _status = status;
            if (volumePercent is not null)
            {
                _volume = volumePercent.Value;
            }

            if (track is not null)
            {
                _track = track;
            }
            else if (status != PlaybackStatus.Stopped)
            {
                _track ??= DefaultTrack;
            }
        }
    }

    private static readonly MusicTrack DefaultTrack = new(
        Id: "mock-track",
        Title: "Mock Track",
        Artist: "Mock Artist",
        Album: null);
}
