using Core.Actions;
using Core.Configuration;
using Core.Models;
using Core.Music;
using Microsoft.Extensions.Logging;

namespace Core.Actions.Spotify;

/// <summary>
/// Compatibility ActionMap entry for <c>spotify.control_profile</c> during the music-key migration.
/// </summary>
public sealed class SpotifyControlProfileAction : IEventAction
{
    private readonly MusicControlProfileAction _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpotifyControlProfileAction"/> class.
    /// </summary>
    /// <param name="playback">The session playback coordinator.</param>
    /// <param name="controlProfileService">The control-profile store.</param>
    /// <param name="logger">The logger used when a command fails.</param>
    public SpotifyControlProfileAction(
        IMusicPlaybackControl playback,
        IControlProfileService controlProfileService,
        ILogger<SpotifyControlProfileAction> logger)
    {
        _inner = new MusicControlProfileAction(
            playback,
            controlProfileService,
            logger,
            MusicControlProfileAction.LegacySpotifyKey);
    }

    /// <inheritdoc />
    public string Key => _inner.Key;

    /// <inheritdoc />
    public Task ExecuteAsync(NormalizedEvent normalizedEvent, CancellationToken cancellationToken = default)
        => _inner.ExecuteAsync(normalizedEvent, cancellationToken);
}
