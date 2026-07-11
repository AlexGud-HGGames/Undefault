using GsiHost.Dtos;
using GsiHost.Tooling.Timeline;
using Microsoft.Extensions.Logging;

namespace GsiHost.Services;

/// <summary>
/// Logs Dota 2 GSI ticks to the timeline as discrete events instead of running them through the
/// CS2 rules-engine pipeline. This is a first, minimal slice (UND-80): there is no
/// <c>IGameAdapter&lt;DotaGsiPayloadDto&gt;</c>, no neutral context, and no Spotify side effects
/// wired to Dota yet — see docs/ingestion-spec-cs2-dota.md and UND-45 for the follow-up scope.
/// </summary>
/// <remarks>
/// Detects three transitions from consecutive POSTs: <c>map.game_state</c> changes,
/// <c>hero.alive</c> flips (death / respawn), and <c>map.paused</c> flips. The first payload in a
/// session establishes each baseline without recording a transition, mirroring
/// <see cref="PlaybackStateObserver"/>'s approach for Spotify play/pause detection.
/// </remarks>
public sealed class DotaGsiLoggingService
{
    private readonly TimelineCaptureService _timeline;
    private readonly ILogger<DotaGsiLoggingService> _logger;
    private readonly object _lock = new();

    private string? _lastGameState;
    private bool? _lastHeroAlive;
    private bool? _lastPaused;
    private bool _hasLoggedConnection;

    public DotaGsiLoggingService(TimelineCaptureService timeline, ILogger<DotaGsiLoggingService> logger)
    {
        _timeline = timeline;
        _logger = logger;
    }

    /// <summary>Processes one Dota 2 GSI POST body, recording any detected transitions.</summary>
    public void Process(DotaGsiPayloadDto payload)
    {
        var timestamp = DateTimeOffset.UtcNow;

        lock (_lock)
        {
            if (!_hasLoggedConnection)
            {
                _hasLoggedConnection = true;
                LogFirstDotaConnection();
            }

            EvaluateGameState(payload.Map?.GameState, timestamp);
            EvaluateBoolTransition(
                payload.Hero?.Alive,
                ref _lastHeroAlive,
                onTrueEventKey: TimelineDotaEvents.HeroRespawned,
                onFalseEventKey: TimelineDotaEvents.HeroDied,
                onTrueVerb: "hero respawned",
                onFalseVerb: "hero died",
                timestamp);
            EvaluateBoolTransition(
                payload.Map?.Paused,
                ref _lastPaused,
                onTrueEventKey: TimelineDotaEvents.Paused,
                onFalseEventKey: TimelineDotaEvents.Resumed,
                onTrueVerb: "paused",
                onFalseVerb: "resumed",
                timestamp);
        }
    }

    /// <summary>
    /// Clears transition baselines so the next Dota GSI payload re-establishes state without
    /// emitting a spurious transition. Called from <see cref="GsiResetService"/> on
    /// <c>POST /gsi/reset</c> (session boundary). Does not re-print the console connection banner.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _lastGameState = null;
            _lastHeroAlive = null;
            _lastPaused = null;
        }
    }

    private void EvaluateGameState(string? gameState, DateTimeOffset timestamp)
    {
        if (string.IsNullOrWhiteSpace(gameState)
            || string.Equals(gameState, _lastGameState, StringComparison.Ordinal))
        {
            return;
        }

        var detail = _lastGameState is null ? gameState : $"{_lastGameState} -> {gameState}";
        _lastGameState = gameState;

        _timeline.RecordDotaEvent(TimelineDotaEvents.GameStateChanged, timestamp, detail);
        _logger.LogInformation("Dota game state: {Detail}", detail);
    }

    private void EvaluateBoolTransition(
        bool? current,
        ref bool? last,
        string onTrueEventKey,
        string onFalseEventKey,
        string onTrueVerb,
        string onFalseVerb,
        DateTimeOffset timestamp)
    {
        if (current is null)
        {
            return;
        }

        if (last is null)
        {
            // First usable observation establishes the baseline; no transition to record.
            last = current;
            return;
        }

        if (last.Value == current.Value)
        {
            return;
        }

        last = current;
        var eventKey = current.Value ? onTrueEventKey : onFalseEventKey;
        var verb = current.Value ? onTrueVerb : onFalseVerb;
        _timeline.RecordDotaEvent(eventKey, timestamp);
        _logger.LogInformation("Dota {Verb} at {Timestamp:O}", verb, timestamp);
    }

    private void LogFirstDotaConnection()
    {
        Console.WriteLine();
        Console.WriteLine("============================================================");
        Console.WriteLine(" Dota 2 GSI connected — receiving live game state from Dota 2.");
        Console.WriteLine("============================================================");
        Console.WriteLine();
        _logger.LogInformation("Dota 2 GSI connected.");
    }
}
