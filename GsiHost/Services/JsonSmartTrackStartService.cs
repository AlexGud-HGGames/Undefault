using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Core.Configuration;
using Core.Spotify;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GsiHost.Services;

public sealed class JsonSmartTrackStartService : ISmartTrackStartService
{
    private readonly SmartTrackStartOptions _options;
    private readonly IProfileService _profileService;
    private readonly ILogger<JsonSmartTrackStartService> _logger;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private Dictionary<string, SmartTrackStartEntry> _entriesByUri = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, SmartTrackStartEntry> _entriesByTrackId = new(StringComparer.OrdinalIgnoreCase);
    private bool _isLoaded;

    public JsonSmartTrackStartService(
        IWebHostEnvironment environment,
        IProfileService profileService,
        IOptions<SmartTrackStartOptions> options,
        ILogger<JsonSmartTrackStartService> logger)
    {
        FilePath = Path.Combine(environment.ContentRootPath, "smart-track-starts.json");
        _profileService = profileService;
        _options = options?.Value ?? new SmartTrackStartOptions();
        _logger = logger;
    }

    public string FilePath { get; }

    public async Task WarmAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !_options.PreloadOnStartup)
        {
            return;
        }

        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        var profiles = await _profileService.GetAsync(cancellationToken).ConfigureAwait(false);
        var activeProfile = profiles.Profiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, profiles.ActiveProfileId, StringComparison.OrdinalIgnoreCase))
            ?? profiles.Profiles.FirstOrDefault();

        if (activeProfile is null)
        {
            _logger.LogInformation("Smart Track Start is enabled, but no active track profile is available.");
            return;
        }

        var trackUris = activeProfile.Rules
            .SelectMany(rule => rule.Tracks)
            .Where(track => !string.IsNullOrWhiteSpace(track))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var primedCount = trackUris.Count(HasEntryForUri);
        _logger.LogInformation(
            "Smart Track Start ready for {PrimedCount}/{TrackCount} track(s) in active profile {ProfileId}.",
            primedCount,
            trackUris.Count,
            activeProfile.Id);
    }

    public async Task<int?> ResolveStartPositionMsAsync(string trackUri, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(trackUri))
        {
            return null;
        }

        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        if (_entriesByUri.TryGetValue(trackUri, out var entryByUri))
        {
            return entryByUri.StartPositionMs;
        }

        var trackId = SmartTrackStartEntry.ParseTrackId(trackUri);
        if (!string.IsNullOrWhiteSpace(trackId)
            && _entriesByTrackId.TryGetValue(trackId, out var entryByTrackId))
        {
            return entryByTrackId.StartPositionMs;
        }

        return null;
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isLoaded)
        {
            return;
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isLoaded)
            {
                return;
            }

            var config = await ReadOrCreateConfigAsync(cancellationToken).ConfigureAwait(false);
            var normalized = NormalizeAndValidate(config);

            _entriesByUri = normalized.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.TrackUri))
                .GroupBy(entry => entry.TrackUri!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            _entriesByTrackId = normalized.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.TrackId))
                .GroupBy(entry => entry.TrackId!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            _isLoaded = true;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<SmartTrackStartsConfig> ReadOrCreateConfigAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(FilePath))
        {
            var defaultConfig = BuildDefaultConfig();
            await WriteAsync(defaultConfig, cancellationToken).ConfigureAwait(false);
            return defaultConfig;
        }

        var content = await File.ReadAllTextAsync(FilePath, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(content))
        {
            var defaultConfig = BuildDefaultConfig();
            await WriteAsync(defaultConfig, cancellationToken).ConfigureAwait(false);
            return defaultConfig;
        }

        var config = JsonSerializer.Deserialize<SmartTrackStartsConfig>(
            content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return config ?? BuildDefaultConfig();
    }

    private async Task WriteAsync(SmartTrackStartsConfig config, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(FilePath, json, cancellationToken).ConfigureAwait(false);
    }

    private bool HasEntryForUri(string trackUri)
    {
        if (_entriesByUri.ContainsKey(trackUri))
        {
            return true;
        }

        var trackId = SmartTrackStartEntry.ParseTrackId(trackUri);
        return !string.IsNullOrWhiteSpace(trackId) && _entriesByTrackId.ContainsKey(trackId);
    }

    private static SmartTrackStartsConfig BuildDefaultConfig()
    {
        return new SmartTrackStartsConfig(new List<SmartTrackStartEntry>());
    }

    private static SmartTrackStartsConfig NormalizeAndValidate(SmartTrackStartsConfig? config)
    {
        if (config is null || config.Entries is null || config.Entries.Count == 0)
        {
            return BuildDefaultConfig();
        }

        var normalizedEntries = config.Entries
            .Select(NormalizeEntry)
            .ToList();

        Validate(normalizedEntries);
        return new SmartTrackStartsConfig(normalizedEntries);
    }

    private static SmartTrackStartEntry NormalizeEntry(SmartTrackStartEntry entry)
    {
        return entry with
        {
            TrackUri = entry.TrackUri?.Trim(),
            TrackId = entry.TrackId?.Trim(),
            CueLabel = string.IsNullOrWhiteSpace(entry.CueLabel) ? null : entry.CueLabel.Trim()
        };
    }

    private static void Validate(IReadOnlyCollection<SmartTrackStartEntry> entries)
    {
        var seenUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenTrackIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.TrackUri) && string.IsNullOrWhiteSpace(entry.TrackId))
            {
                throw new ArgumentException("Smart Track Start entry must include TrackUri or TrackId.");
            }

            if (entry.StartPositionMs < 0)
            {
                throw new ArgumentException("Smart Track Start entry StartPositionMs must be zero or greater.");
            }

            if (!string.IsNullOrWhiteSpace(entry.TrackUri) && !seenUris.Add(entry.TrackUri))
            {
                throw new ArgumentException($"Duplicate Smart Track Start TrackUri '{entry.TrackUri}'.");
            }

            if (!string.IsNullOrWhiteSpace(entry.TrackId) && !seenTrackIds.Add(entry.TrackId))
            {
                throw new ArgumentException($"Duplicate Smart Track Start TrackId '{entry.TrackId}'.");
            }
        }
    }
}
