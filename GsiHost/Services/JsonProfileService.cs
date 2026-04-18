using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Core.Configuration;
using Core.Models;
using Microsoft.Extensions.Logging;

namespace GsiHost.Services;

public sealed class JsonProfileService : IProfileService
{
    private readonly string _filePath;
    private readonly ILogger<JsonProfileService> _logger;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public JsonProfileService(IWebHostEnvironment environment, ILogger<JsonProfileService> logger)
    {
        _filePath = Path.Combine(environment.ContentRootPath, "profiles.json");
        _logger = logger;
    }

    public async Task<MusicProfilesConfig> GetAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_filePath))
            {
                return BuildDefaultConfig();
            }

            var content = await File.ReadAllTextAsync(_filePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                return BuildDefaultConfig();
            }

            var config = JsonSerializer.Deserialize<MusicProfilesConfig>(
                content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return NormalizeAndValidate(config);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read profiles config, using default.");
            return BuildDefaultConfig();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task SaveAsync(MusicProfilesConfig config, CancellationToken cancellationToken = default)
    {
        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        var normalized = NormalizeAndValidate(config);

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(
                normalized,
                new JsonSerializerOptions { WriteIndented = true });

            await File.WriteAllTextAsync(_filePath, json, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private static MusicProfilesConfig BuildDefaultConfig()
    {
        return new MusicProfilesConfig(
            "default",
            new List<MusicProfile>
            {
                new("default", "Default", new List<EventTrackRule>())
            });
    }

    private static MusicProfilesConfig NormalizeAndValidate(MusicProfilesConfig? config)
    {
        if (config is null || config.Profiles is null || config.Profiles.Count == 0)
        {
            return BuildDefaultConfig();
        }

        var profiles = config.Profiles
            .Select(NormalizeProfile)
            .ToList();

        var activeProfileId = string.IsNullOrWhiteSpace(config.ActiveProfileId)
            ? profiles[0].Id
            : config.ActiveProfileId.Trim();

        if (!profiles.Any(profile => string.Equals(profile.Id, activeProfileId, StringComparison.OrdinalIgnoreCase)))
        {
            activeProfileId = profiles[0].Id;
        }

        var normalized = new MusicProfilesConfig(activeProfileId, profiles);
        Validate(normalized);
        return normalized;
    }

    private static MusicProfile NormalizeProfile(MusicProfile profile)
    {
        var id = string.IsNullOrWhiteSpace(profile.Id)
            ? Guid.NewGuid().ToString("N")
            : profile.Id.Trim();
        var name = string.IsNullOrWhiteSpace(profile.Name)
            ? "Untitled Profile"
            : profile.Name.Trim();
        var rules = (profile.Rules ?? new List<EventTrackRule>())
            .Select(NormalizeRule)
            .ToList();

        return new MusicProfile(id, name, rules);
    }

    private static EventTrackRule NormalizeRule(EventTrackRule rule)
    {
        var eventKey = EventKeys.Normalize(rule.EventKey);
        var tracks = (rule.Tracks ?? new List<string>())
            .Select(track => track?.Trim() ?? string.Empty)
            .Where(track => !string.IsNullOrWhiteSpace(track))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new EventTrackRule(eventKey, tracks);
    }

    private static void Validate(MusicProfilesConfig config)
    {
        var seenProfileIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in config.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Id))
            {
                throw new ArgumentException("Profile id is required.", nameof(config));
            }

            if (!seenProfileIds.Add(profile.Id))
            {
                throw new ArgumentException($"Duplicate profile id '{profile.Id}'.", nameof(config));
            }

            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                throw new ArgumentException($"Profile '{profile.Id}' must have a name.", nameof(config));
            }

            var seenEventKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in profile.Rules)
            {
                if (string.IsNullOrWhiteSpace(rule.EventKey))
                {
                    throw new ArgumentException($"Profile '{profile.Name}' contains a rule with an empty event key.", nameof(config));
                }

                if (!seenEventKeys.Add(rule.EventKey))
                {
                    throw new ArgumentException(
                        $"Profile '{profile.Name}' contains duplicate event key '{rule.EventKey}'.",
                        nameof(config));
                }

                if (rule.Tracks.Count == 0)
                {
                    throw new ArgumentException(
                        $"Profile '{profile.Name}' event '{rule.EventKey}' must include at least one track URI.",
                        nameof(config));
                }
            }
        }
    }
}
