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

            if (config is null || config.Profiles is null || config.Profiles.Count == 0)
            {
                return BuildDefaultConfig();
            }

            var activeId = string.IsNullOrWhiteSpace(config.ActiveProfileId)
                ? config.Profiles[0].Id
                : config.ActiveProfileId;

            return config with { ActiveProfileId = activeId };
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

        Validate(config);

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(
                config,
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
                new("default", "Default", new Dictionary<EventType, EventRule>())
            });
    }

    private static void Validate(MusicProfilesConfig config)
    {
        foreach (var profile in config.Profiles)
        {
            foreach (var rule in profile.Rules.Values)
            {
                if (rule.Volume is < 0 or > 100)
                {
                    throw new ArgumentOutOfRangeException(nameof(config), "Volume must be between 0 and 100");
                }
            }
        }
    }
}
