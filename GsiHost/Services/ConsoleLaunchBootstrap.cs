using System.Runtime.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GsiHost.Services;

public sealed record ConsoleLaunchSettings(
    string GsiBaseUrl,
    string RedirectUri,
    bool IsQuickLaunch,
    bool SkipCs2Setup,
    bool SkipSmartTrackWarmup,
    bool HasSpotifyCredentials,
    bool PromptedForCredentials,
    bool LoadedFromEncryptedStore,
    bool SavedToEncryptedStore,
    bool ClearedEncryptedStore,
    bool ResetEncryptedStoreRequested,
    bool LegacyClientSecretEnvVarPresent,
    string EncryptedStorePath,
    IReadOnlyDictionary<string, string?> ConfigurationOverrides
);

/// <summary>
/// Locally-persisted Spotify identity. Post-UND-47 this is just the public
/// <c>client_id</c>; PKCE has no <c>client_secret</c> half. The record name and
/// encrypted-store path are kept for backwards compatibility with already-written
/// files so a tester upgrading does not need to manually run
/// <c>--clear-spotify-secrets</c>; legacy fields in those files are simply ignored
/// at load time.
/// </summary>
public sealed record SpotifyLocalSecrets(
    string ClientId
);

public interface IConsoleCredentialPrompter
{
    string? ReadValue(string prompt);
    string? ReadSecret(string prompt);
}

public interface ISpotifySecretStore
{
    string FilePath { get; }
    bool Exists();
    SpotifyLocalSecrets? TryLoad();
    void Save(SpotifyLocalSecrets secrets);
    void Delete();
}

public static class ConsoleLaunchBootstrap
{
    public const string DefaultGsiBaseUrl = "http://127.0.0.1:5292";
    private const string DefaultCallbackPath = "/callback";
    private const string ResetSecretsArg = "--reset-spotify-secrets";
    private const string ClearSecretsArg = "--clear-spotify-secrets";
    private const string QuickLaunchArg = "--quick";
    private const string SkipCs2SetupArg = "--skip-cs2-setup";
    private const string SkipSmartTrackWarmupArg = "--skip-smart-track-warmup";
    private const string UseMockSpotifyArg = "--use-mock-spotify";
    private const string UseRealSpotifyArg = "--use-real-spotify";
    private const string IntentCaptureArg = "--intent-capture";
    private const string ScenarioPlaybackArg = "--scenario-playback";

    public static ConsoleLaunchSettings Apply(WebApplicationBuilder builder, string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Encrypted Spotify secret storage currently supports Windows only.");
        }

        var settings = Prepare(
            builder.Configuration,
            args,
            IsInteractiveConsole(),
            new SystemConsoleCredentialPrompter(),
            CreateDefaultSecretStore(),
            NullLogger.Instance);

        builder.Configuration.AddInMemoryCollection(settings.ConfigurationOverrides);
        builder.WebHost.UseUrls(settings.GsiBaseUrl);
        return settings;
    }

    public static ConsoleLaunchSettings Prepare(
        IConfiguration configuration,
        IReadOnlyCollection<string> args,
        bool isInteractiveConsole,
        IConsoleCredentialPrompter prompter,
        ISpotifySecretStore secretStore,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(prompter);
        ArgumentNullException.ThrowIfNull(secretStore);

        logger ??= NullLogger.Instance;

        var gsiBaseUrl = NormalizeBaseUrl(configuration["Gsi:Url"]);
        var redirectUri = NormalizeRedirectUri(configuration["Spotify:RedirectUri"], gsiBaseUrl);

        var requestedUseMockSpotify = HasArg(args, UseMockSpotifyArg);
        var requestedUseRealSpotify = HasArg(args, UseRealSpotifyArg);
        var requestedIntentCapture = HasArg(args, IntentCaptureArg);
        var requestedScenarioPlayback = HasArg(args, ScenarioPlaybackArg);
        var isQuickLaunch = HasArg(args, QuickLaunchArg) && !requestedUseRealSpotify;
        var skipCs2Setup = isQuickLaunch || HasArg(args, SkipCs2SetupArg);
        var skipSmartTrackWarmup = isQuickLaunch || HasArg(args, SkipSmartTrackWarmupArg);
        var useMockSpotify = requestedUseRealSpotify
            ? false
            : (requestedUseMockSpotify || isQuickLaunch);

        var resetEncryptedStoreRequested = HasArg(args, ResetSecretsArg);
        var clearEncryptedStoreRequested = HasArg(args, ClearSecretsArg);
        var clearedEncryptedStore = false;

        if (clearEncryptedStoreRequested)
        {
            // UND-47: with PKCE there is no client_secret to clear. The flag still
            // wipes the cached client_id (and any legacy client_secret blob) so a
            // tester rotating apps gets a fresh prompt next launch.
            if (secretStore.Exists())
            {
                secretStore.Delete();
                clearedEncryptedStore = true;
            }
        }

        var envClientId = Environment.GetEnvironmentVariable("CLIENT_ID");
        var envClientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
        var legacyClientSecretEnvVarPresent = !string.IsNullOrWhiteSpace(envClientSecret);

        if (legacyClientSecretEnvVarPresent)
        {
            // UND-47: CLIENT_SECRET is no longer read or stored. Surfacing the
            // *presence* (never the value) helps testers update their environment
            // after the PKCE switch.
            logger.LogDebug(
                "CLIENT_SECRET environment variable is set but is ignored — Spotify OAuth uses PKCE and does not require a client secret.");
        }

        SpotifyLocalSecrets? storedSecrets = null;
        if (!useMockSpotify
            && string.IsNullOrWhiteSpace(envClientId)
            && !resetEncryptedStoreRequested)
        {
            storedSecrets = secretStore.TryLoad();
        }

        string clientId;

        if (useMockSpotify)
        {
            // Quick/mock mode never prompts for OAuth and never reads/writes the
            // encrypted store.
            clientId = string.Empty;
        }
        else
        {
            clientId = FirstNonEmpty(
                envClientId,
                storedSecrets?.ClientId,
                configuration["Spotify:ClientId"]) ?? string.Empty;
        }

        var promptedForCredentials = false;
        var savedToEncryptedStore = false;
        var loadedFromEncryptedStore = !useMockSpotify && storedSecrets is not null;

        var shouldPromptForClientId = isInteractiveConsole && (
            resetEncryptedStoreRequested ||
            string.IsNullOrWhiteSpace(clientId))
            && !useMockSpotify;

        if (shouldPromptForClientId)
        {
            clientId = prompter.ReadValue("Spotify Client ID") ?? string.Empty;
            promptedForCredentials = !string.IsNullOrWhiteSpace(clientId);

            if (!string.IsNullOrWhiteSpace(clientId))
            {
                secretStore.Save(new SpotifyLocalSecrets(clientId));
                savedToEncryptedStore = true;
                loadedFromEncryptedStore = false;
            }
        }

        var overrides = new Dictionary<string, string?>
        {
            ["UseMockSpotify"] = useMockSpotify ? "true" : "false",
            ["Gsi:Url"] = gsiBaseUrl,
            ["Spotify:RedirectUri"] = redirectUri
        };

        if (requestedIntentCapture && !requestedScenarioPlayback)
        {
            overrides["Runtime:Mode"] = "intent_capture";
        }
        else if (requestedScenarioPlayback)
        {
            overrides["Runtime:Mode"] = "scenario_playback";
        }

        if (!useMockSpotify && !string.IsNullOrWhiteSpace(clientId))
        {
            overrides["Spotify:ClientId"] = clientId;
        }

        return new ConsoleLaunchSettings(
            GsiBaseUrl: gsiBaseUrl,
            RedirectUri: redirectUri,
            IsQuickLaunch: isQuickLaunch,
            SkipCs2Setup: skipCs2Setup,
            SkipSmartTrackWarmup: skipSmartTrackWarmup,
            HasSpotifyCredentials: !string.IsNullOrWhiteSpace(clientId),
            PromptedForCredentials: promptedForCredentials,
            LoadedFromEncryptedStore: loadedFromEncryptedStore,
            SavedToEncryptedStore: savedToEncryptedStore,
            ClearedEncryptedStore: clearedEncryptedStore,
            ResetEncryptedStoreRequested: resetEncryptedStoreRequested,
            LegacyClientSecretEnvVarPresent: legacyClientSecretEnvVarPresent,
            EncryptedStorePath: secretStore.FilePath,
            ConfigurationOverrides: overrides);
    }

    private static bool IsInteractiveConsole()
    {
        return Environment.UserInteractive && !Console.IsInputRedirected;
    }

    [SupportedOSPlatform("windows")]
    private static ISpotifySecretStore CreateDefaultSecretStore()
    {
        return new WindowsProtectedSpotifySecretStore();
    }

    private static bool HasArg(IEnumerable<string> args, string expectedArg)
    {
        return args.Any(arg => string.Equals(arg, expectedArg, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeBaseUrl(string? configuredBaseUrl)
    {
        if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var uri))
        {
            return DefaultGsiBaseUrl;
        }

        var builder = new UriBuilder(uri)
        {
            Host = NormalizeLoopbackHost(uri.Host),
            Path = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty
        };

        if (builder.Port <= 0)
        {
            builder.Port = 5292;
        }

        if (!builder.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            builder.Scheme = Uri.UriSchemeHttp;
        }

        return builder.Uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private static string NormalizeRedirectUri(string? configuredRedirectUri, string gsiBaseUrl)
    {
        if (!Uri.TryCreate(configuredRedirectUri, UriKind.Absolute, out var uri))
        {
            return $"{gsiBaseUrl}{DefaultCallbackPath}";
        }

        var builder = new UriBuilder(uri)
        {
            Host = NormalizeLoopbackHost(uri.Host)
        };

        return builder.Uri.ToString();
    }

    private static string NormalizeLoopbackHost(string host)
    {
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            ? "127.0.0.1"
            : host;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private sealed class SystemConsoleCredentialPrompter : IConsoleCredentialPrompter
    {
        public string? ReadValue(string prompt)
        {
            Console.Write($"{prompt}: ");
            return Console.ReadLine()?.Trim();
        }

        public string? ReadSecret(string prompt)
        {
            Console.Write($"{prompt}: ");
            var buffer = new List<char>();

            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return new string(buffer.ToArray()).Trim();
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (buffer.Count == 0)
                    {
                        continue;
                    }

                    buffer.RemoveAt(buffer.Count - 1);
                    Console.Write("\b \b");
                    continue;
                }

                if (char.IsControl(key.KeyChar))
                {
                    continue;
                }

                buffer.Add(key.KeyChar);
                Console.Write('*');
            }
        }
    }
}
