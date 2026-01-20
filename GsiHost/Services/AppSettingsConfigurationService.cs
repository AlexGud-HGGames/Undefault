using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Core.Configuration;
using Microsoft.Extensions.Logging;

namespace GsiHost.Services;

public sealed class AppSettingsConfigurationService : IConfigurationService
{
    private readonly string _filePath;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AppSettingsConfigurationService> _logger;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public AppSettingsConfigurationService(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<AppSettingsConfigurationService> logger)
    {
        _filePath = Path.Combine(environment.ContentRootPath, "appsettings.json");
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<SystemConfig> GetAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var root = await ReadRootAsync(cancellationToken);
            var spotify = ParseSpotify(root);
            var useMockSpotify = root["UseMockSpotify"]?.GetValue<bool>() ?? false;

            var url = _configuration["Urls"] ?? _configuration["ASPNETCORE_URLS"];
            var gsi = new GsiConfig("POST", "/gsi", url);

            return new SystemConfig(spotify, gsi, useMockSpotify);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task SaveAsync(SystemConfig config, CancellationToken cancellationToken = default)
    {
        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var root = await ReadRootAsync(cancellationToken);
            root["UseMockSpotify"] = config.UseMockSpotify;

            var spotifyNode = root["Spotify"] as JsonObject ?? new JsonObject();
            var clientSecret = spotifyNode["ClientSecret"]?.GetValue<string>();
            spotifyNode["ClientId"] = config.Spotify.ClientId;
            spotifyNode["RedirectUri"] = config.Spotify.RedirectUri;
            spotifyNode["Scopes"] = BuildStringArrayNode(config.Spotify.Scopes);
            if (!string.IsNullOrWhiteSpace(clientSecret))
            {
                spotifyNode["ClientSecret"] = clientSecret;
            }

            root["Spotify"] = spotifyNode;

            var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<JsonObject> ReadRootAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogWarning("Config file not found at {Path}, creating new object", _filePath);
            return new JsonObject();
        }

        var content = await File.ReadAllTextAsync(_filePath, cancellationToken);
        var node = JsonNode.Parse(content);
        return node as JsonObject ?? new JsonObject();
    }

    private static SpotifySystemConfig ParseSpotify(JsonObject root)
    {
        var spotifyNode = root["Spotify"] as JsonObject;
        var clientId = spotifyNode?["ClientId"]?.GetValue<string>() ?? string.Empty;
        var redirectUri = spotifyNode?["RedirectUri"]?.GetValue<string>() ?? string.Empty;
        var clientSecret = spotifyNode?["ClientSecret"]?.GetValue<string>();
        var scopes = spotifyNode?["Scopes"] is JsonArray scopesArray
            ? scopesArray
                .Select(item => item?.GetValue<string>() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray()
            : Array.Empty<string>();

        return new SpotifySystemConfig(clientId, redirectUri, scopes, clientSecret);
    }

    private static JsonArray BuildStringArrayNode(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }
}
