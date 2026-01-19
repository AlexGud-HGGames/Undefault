using System.Text.Json;
using System.Text.Json.Nodes;
using System.Linq;
using Core.Configuration;
using Core.Models;
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

    public async Task<AppConfig> GetAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var root = await ReadRootAsync(cancellationToken);
            var rulesEngine = ParseRulesEngine(root);
            var spotify = ParseSpotify(root);
            var spotifyActions = ParseSpotifyActions(root);

            var url = _configuration["Urls"] ?? _configuration["ASPNETCORE_URLS"];
            var gsiEndpoint = new GsiEndpointInfo("POST", "/gsi", url);

            return new AppConfig(rulesEngine, spotify, spotifyActions, gsiEndpoint);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task SaveAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        Validate(config);

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var root = await ReadRootAsync(cancellationToken);

            root["RulesEngine"] = BuildRulesEngineNode(config.RulesEngine);
            root["SpotifyActions"] = BuildSpotifyActionsNode(config.SpotifyActions);

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

    private static RulesEngineConfig ParseRulesEngine(JsonObject root)
    {
        var map = new Dictionary<EventType, List<string>>();
        var rulesNode = root["RulesEngine"] as JsonObject;
        var actionMapNode = rulesNode?["ActionMap"] as JsonObject;
        if (actionMapNode is not null)
        {
            foreach (var (key, value) in actionMapNode)
            {
                if (!Enum.TryParse<EventType>(key, true, out var eventType))
                {
                    continue;
                }

                var list = new List<string>();
                if (value is JsonArray array)
                {
                    foreach (var item in array)
                    {
                        var action = item?.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(action))
                        {
                            list.Add(action);
                        }
                    }
                }

                map[eventType] = list;
            }
        }

        return new RulesEngineConfig(map);
    }

    private static SpotifyPublicConfig ParseSpotify(JsonObject root)
    {
        var spotifyNode = root["Spotify"] as JsonObject;
        var clientId = spotifyNode?["ClientId"]?.GetValue<string>() ?? string.Empty;
        var redirectUri = spotifyNode?["RedirectUri"]?.GetValue<string>() ?? string.Empty;
        var scopes = spotifyNode?["Scopes"] is JsonArray scopesArray
            ? scopesArray.Select(item => item?.GetValue<string>() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray()
            : Array.Empty<string>();

        return new SpotifyPublicConfig(clientId, redirectUri, scopes);
    }

    private static SpotifyActionsConfig ParseSpotifyActions(JsonObject root)
    {
        var playlistMap = new Dictionary<EventType, List<string>>();
        var volumeMap = new Dictionary<EventType, int>();

        var actionsNode = root["SpotifyActions"] as JsonObject;
        var playlistNode = actionsNode?["EventPlaylistMap"] as JsonObject;
        if (playlistNode is not null)
        {
            foreach (var (key, value) in playlistNode)
            {
                if (!Enum.TryParse<EventType>(key, true, out var eventType))
                {
                    continue;
                }

                var uris = ParseStringList(value);
                if (uris.Count > 0)
                {
                    playlistMap[eventType] = uris;
                }
            }
        }

        var volumeNode = actionsNode?["EventVolumeMap"] as JsonObject;
        if (volumeNode is not null)
        {
            foreach (var (key, value) in volumeNode)
            {
                if (!Enum.TryParse<EventType>(key, true, out var eventType))
                {
                    continue;
                }

                if (value is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var volume))
                {
                    volumeMap[eventType] = volume;
                }
            }
        }

        var defaultPlaylists = ParseStringList(actionsNode?["DefaultPlaylistUris"]);
        if (defaultPlaylists.Count == 0)
        {
            defaultPlaylists = ParseStringList(actionsNode?["DefaultPlaylistUri"]);
        }
        var defaultVolume = actionsNode?["DefaultVolume"]?.GetValue<int?>();

        return new SpotifyActionsConfig(
            playlistMap,
            volumeMap,
            defaultPlaylists,
            defaultVolume
        );
    }

    private static JsonObject BuildRulesEngineNode(RulesEngineConfig config)
    {
        var actionMapNode = new JsonObject();
        foreach (var (eventType, actions) in config.ActionMap)
        {
            var array = new JsonArray();
            foreach (var action in actions)
            {
                array.Add(action);
            }

            actionMapNode[eventType.ToString()] = array;
        }

        return new JsonObject
        {
            ["ActionMap"] = actionMapNode
        };
    }

    private static JsonObject BuildSpotifyActionsNode(SpotifyActionsConfig config)
    {
        var playlistNode = new JsonObject();
        foreach (var (eventType, uris) in config.EventPlaylistMap)
        {
            playlistNode[eventType.ToString()] = BuildStringArrayNode(uris);
        }

        var volumeNode = new JsonObject();
        foreach (var (eventType, volume) in config.EventVolumeMap)
        {
            volumeNode[eventType.ToString()] = volume;
        }

        return new JsonObject
        {
            ["EventPlaylistMap"] = playlistNode,
            ["EventVolumeMap"] = volumeNode,
            ["DefaultPlaylistUris"] = BuildStringArrayNode(config.DefaultPlaylistUris),
            ["DefaultVolume"] = config.DefaultVolume is null ? null : JsonValue.Create(config.DefaultVolume)
        };
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

    private static List<string> ParseStringList(JsonNode? node)
    {
        if (node is null)
        {
            return new List<string>();
        }

        if (node is JsonArray array)
        {
            return array
                .Select(item => item?.GetValue<string>() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
        }

        if (node is JsonValue valueNode && valueNode.TryGetValue<string>(out var value))
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return new List<string> { value };
            }
        }

        return new List<string>();
    }

    private static void Validate(AppConfig config)
    {
        foreach (var volume in config.SpotifyActions.EventVolumeMap.Values)
        {
            if (volume is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(config), "Volume must be between 0 and 100");
            }
        }

        if (config.SpotifyActions.DefaultVolume is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(config), "Default volume must be between 0 and 100");
        }
    }
}
