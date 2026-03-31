using Core.Actions;
using Core.Actions.Spotify;
using Core.Configuration;
using Core.Diff;
using Core.Rules;
using Core.Services;
using Core.Spotify;
using Core.Stores;
using GsiHost.Dtos;
using GsiHost.Mapping;
using GsiHost.Mapping.Modules;
using GsiHost.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var consoleLaunchSettings = ConsoleLaunchBootstrap.Apply(builder, args);

builder.Services.AddSingleton<GsiSnapshotMapper>();
builder.Services.AddSingleton<SnapshotDiffer>();
builder.Services.AddSingleton<EventDetector>(sp =>
    new EventDetector(sp.GetRequiredService<IOptions<EventDetectorOptions>>().Value));
builder.Services.AddSingleton<ISnapshotStore, InMemorySnapshotStore>();
builder.Services.AddSingleton<IEventAction, LogEventAction>();
builder.Services.AddSingleton<IEventAction, SpotifyProfileAction>();
builder.Services.AddSingleton<IEventAction, SpotifyControlProfileAction>();
builder.Services.AddSingleton<IEventAction, SpotifyVolumeDuckAction>();
builder.Services.AddSingleton<IPlaybackPolicy, NoOpPlaybackPolicy>();
builder.Services.AddSingleton<IRulesEngine, RulesEngine>();
builder.Services.AddSingleton<GsiProcessingService>();
builder.Services.AddSingleton<AppStateService>();
builder.Services.AddSingleton<IAppStateService>(sp => sp.GetRequiredService<AppStateService>());
builder.Services.AddSingleton<IConfigurationService, AppSettingsConfigurationService>();
builder.Services.AddSingleton<IControlProfileService, JsonControlProfileService>();
builder.Services.AddSingleton<IProfileService, JsonProfileService>();
builder.Services.AddSingleton<ICs2SetupService, Cs2SetupService>();
builder.Services.AddSingleton<ISnapshotModuleMapper, VitalsModuleMapper>();
builder.Services.AddSingleton<ISnapshotModuleMapper, PositionModuleMapper>();
builder.Services.AddSingleton<ISnapshotModuleMapper, CombatModuleMapper>();
builder.Services.AddSingleton<ISnapshotModuleMapper, RoundModuleMapper>();

BuildSpotify(builder);

builder.Services.Configure<RulesEngineOptions>(
    builder.Configuration.GetSection("RulesEngine"));
builder.Services.Configure<EventDetectorOptions>(
    builder.Configuration.GetSection("EventDetector"));
builder.Services.Configure<SpotifyClientOptions>(
    builder.Configuration.GetSection("Spotify"));
builder.Services.Configure<SpotifyVolumeDuckOptions>(
    builder.Configuration.GetSection("SpotifyVolumeDuck"));

var app = builder.Build();

await EnsureCs2SetupAsync(app);
var authorizationUrl = LogSpotifyAuthorizationUrl(app);
await WriteConsoleStartupChecklistAsync(app, consoleLaunchSettings, authorizationUrl);

app.MapGet("/", () => "UndefaultIt GSI Host");

app.MapPost("/gsi", async (
    GsiPayloadDto payload,
    GsiProcessingService processor,
    CancellationToken cancellationToken) =>
{
    var events = await processor.ProcessAsync(payload, cancellationToken);
    return Results.Ok(new { events = events.Count });
});

app.MapGet("/status", async (IAppStateService appStateService, CancellationToken cancellationToken) =>
{
    var status = await appStateService.GetCurrentStatusAsync(cancellationToken);
    return Results.Ok(status);
});

app.MapGet("/events", (AppStateService appStateService) => Results.Ok((object?)appStateService.GetRecentEvents()));

app.MapGet("/spotify/status", async (IServiceProvider services, CancellationToken cancellationToken) =>
{
    var spotifyClient = services.GetRequiredService<ISpotifyClient>();
    var oauthService = services.GetService<SpotifyOAuthService>();
    var tokenStorage = services.GetService<ITokenStorage>();

    bool isAuthenticated;
    try
    {
        isAuthenticated = await spotifyClient.IsAuthenticatedAsync(cancellationToken);
    }
    catch
    {
        isAuthenticated = false;
    }

    var accessToken = tokenStorage is null
        ? null
        : await tokenStorage.GetAccessTokenAsync(cancellationToken);
    var expiresAt = tokenStorage is null
        ? null
        : await tokenStorage.GetExpiresAtAsync(cancellationToken);

    return Results.Ok(new
    {
        UseMockSpotify = oauthService is null || tokenStorage is null,
        HasClientCredentials = oauthService?.HasClientCredentials ?? false,
        RedirectUri = oauthService?.RedirectUri,
        IsAuthenticated = isAuthenticated,
        HasAccessToken = !string.IsNullOrWhiteSpace(accessToken),
        ExpiresAt = expiresAt
    });
});

app.MapGet("/config", async (IConfigurationService configService, CancellationToken cancellationToken) =>
{
    var config = await configService.GetAsync(cancellationToken);
    return Results.Ok(config);
});

app.MapPut("/config", async (SystemConfig config, IConfigurationService configService, CancellationToken cancellationToken) =>
{
    await configService.SaveAsync(config, cancellationToken);
    return Results.NoContent();
});

app.MapGet("/control-profiles", async (IControlProfileService controlProfileService, CancellationToken cancellationToken) =>
{
    var profiles = await controlProfileService.GetAsync(cancellationToken);
    return Results.Ok(profiles);
});

app.MapPut("/control-profiles", async (
    ConsoleControlProfilesConfig profiles,
    IControlProfileService controlProfileService,
    CancellationToken cancellationToken) =>
{
    await controlProfileService.SaveAsync(profiles, cancellationToken);
    return Results.NoContent();
});

app.MapGet("/setup/cs2/status", async (ICs2SetupService setupService, CancellationToken cancellationToken) =>
{
    var status = await setupService.GetStatusAsync(cancellationToken);
    return Results.Ok(status);
});

app.MapPost("/setup/cs2/install", async (ICs2SetupService setupService, CancellationToken cancellationToken) =>
{
    var result = await setupService.InstallAsync(cancellationToken);
    return result.Success
        ? Results.Ok(result)
        : Results.BadRequest(result);
});

app.MapGet("/profiles", async (IProfileService profileService, CancellationToken cancellationToken) =>
{
    var profiles = await profileService.GetAsync(cancellationToken);
    return Results.Ok(profiles);
});

app.MapPut("/profiles", async (MusicProfilesConfig profiles, IProfileService profileService, CancellationToken cancellationToken) =>
{
    await profileService.SaveAsync(profiles, cancellationToken);
    return Results.NoContent();
});

app.MapGet("/spotify/authorize", (IServiceProvider services) =>
{
    var oauthService = services.GetService<SpotifyOAuthService>();
    if (oauthService is null)
    {
        return Results.BadRequest("Spotify OAuth is unavailable in mock mode.");
    }

    var state = Guid.NewGuid().ToString("N");
    var url = oauthService.GetAuthorizationUrl(state);
    return Results.Ok(new { url, state });
});

app.MapGet("/callback", async (
    string code,
    IServiceProvider services,
    CancellationToken cancellationToken) =>
{
    return await HandleSpotifyCallbackAsync(code, services, cancellationToken);
});

app.MapGet("/spotify/callback", async (
    string code,
    IServiceProvider services,
    CancellationToken cancellationToken) =>
{
    return await HandleSpotifyCallbackAsync(code, services, cancellationToken);
});

app.Run();

void BuildSpotify(WebApplicationBuilder webApplicationBuilder)
{
    var useMock = webApplicationBuilder.Configuration.GetValue<bool>("UseMockSpotify");

    if (useMock)
    {
        webApplicationBuilder.Services.AddSingleton<ISpotifyClient, MockSpotifyClient>();
        return;
    }

    // Spotify services
    webApplicationBuilder.Services.AddHttpClient("SpotifyApi");
    webApplicationBuilder.Services.AddHttpClient("SpotifyOAuth");
    webApplicationBuilder.Services.AddSingleton<ITokenStorage, InMemoryTokenStorage>();
    webApplicationBuilder.Services.AddSingleton<SpotifyOAuthService>();
    webApplicationBuilder.Services.AddSingleton<ISpotifyClient, SpotifyClient>();

}

static async Task EnsureCs2SetupAsync(WebApplication app)
{
    try
    {
        var setupService = app.Services.GetRequiredService<ICs2SetupService>();
        var result = await setupService.EnsureInstalledAsync();

        if (result.Success)
        {
            app.Logger.LogInformation(
                "CS2 GSI setup ready at {CfgPath} (updated={WasUpdated}, uri={GsiUri})",
                result.CfgPath,
                result.WasUpdated,
                result.GsiUri);
            app.Logger.LogInformation("Console control profile mode active. Edit control-profiles.json to change music behavior.");
            return;
        }

        app.Logger.LogWarning("CS2 GSI setup not ready: {Error}", result.Error);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to auto-configure CS2 GSI");
    }
}

static string? LogSpotifyAuthorizationUrl(WebApplication app)
{
    var oauthService = app.Services.GetService<SpotifyOAuthService>();
    if (oauthService is null)
    {
        return null;
    }

    if (!oauthService.HasClientCredentials)
    {
        app.Logger.LogWarning("Spotify OAuth credentials not found. Provide them in the console once, or set CLIENT_ID and CLIENT_SECRET.");
        return null;
    }

    var authorizationUrl = oauthService.GetAuthorizationUrl();
    Console.WriteLine($"Spotify authorization URL: {authorizationUrl}");
    app.Logger.LogInformation("Spotify authorization URL: {AuthorizationUrl}", authorizationUrl);
    return authorizationUrl;
}

static async Task WriteConsoleStartupChecklistAsync(
    WebApplication app,
    ConsoleLaunchSettings consoleLaunchSettings,
    string? authorizationUrl)
{
    var setupService = app.Services.GetRequiredService<ICs2SetupService>();
    var controlProfileService = app.Services.GetRequiredService<IControlProfileService>();
    var spotifyClient = app.Services.GetRequiredService<ISpotifyClient>();
    var cs2Status = await setupService.GetStatusAsync();
    var controlProfiles = await controlProfileService.GetAsync();
    var activeControlProfile = controlProfiles.Profiles.FirstOrDefault(profile =>
        string.Equals(profile.Id, controlProfiles.ActiveProfileId, StringComparison.OrdinalIgnoreCase))
        ?? controlProfiles.Profiles.FirstOrDefault();

    var spotifyAuthenticated = false;
    try
    {
        spotifyAuthenticated = await spotifyClient.IsAuthenticatedAsync();
    }
    catch
    {
        spotifyAuthenticated = false;
    }

    Console.WriteLine();
    Console.WriteLine("UndefaultIt console startup");
    Console.WriteLine($"- Spotify credentials: {(consoleLaunchSettings.HasSpotifyCredentials ? "ready" : "missing")}");
    Console.WriteLine($"- Prompted for credentials this run: {(consoleLaunchSettings.PromptedForCredentials ? "yes" : "no")}");
    Console.WriteLine($"- Encrypted Spotify secret store: {consoleLaunchSettings.EncryptedStorePath}");
    Console.WriteLine($"- Loaded credentials from encrypted store: {(consoleLaunchSettings.LoadedFromEncryptedStore ? "yes" : "no")}");
    Console.WriteLine($"- Saved credentials to encrypted store this run: {(consoleLaunchSettings.SavedToEncryptedStore ? "yes" : "no")}");
    Console.WriteLine($"- Cleared encrypted store this run: {(consoleLaunchSettings.ClearedEncryptedStore ? "yes" : "no")}");
    Console.WriteLine($"- Spotify redirect URI to register: {consoleLaunchSettings.RedirectUri}");
    Console.WriteLine($"- Spotify authorization URL: {authorizationUrl ?? "unavailable until credentials are provided"}");
    Console.WriteLine($"- CS2 GSI target URL: {cs2Status.GsiUri ?? $"{consoleLaunchSettings.GsiBaseUrl}/gsi"}");
    Console.WriteLine($"- CS2 cfg ready: {(cs2Status.IsReady ? "yes" : "no")}{FormatSuffix(cs2Status.CfgPath)}");
    Console.WriteLine($"- Control profile file: {controlProfileService.FilePath}");
    Console.WriteLine($"- Active control profile: {activeControlProfile?.Name ?? "none"}{FormatSuffix(activeControlProfile?.Id)}");
    Console.WriteLine($"- Spotify authenticated: {(spotifyAuthenticated ? "yes" : "no")}");
    Console.WriteLine("- Spotify playback control requires Premium and an active playback device.");
    Console.WriteLine("- Use --reset-spotify-secrets to overwrite the saved secrets without printing them.");
    Console.WriteLine("- Edit control-profiles.json for pause/resume/duck behavior.");
    Console.WriteLine("- Open /spotify/status, /setup/cs2/status, or /control-profiles for diagnostics.");
    Console.WriteLine();
}

static string FormatSuffix(string? value)
{
    return string.IsNullOrWhiteSpace(value) ? string.Empty : $" ({value})";
}

static async Task<IResult> HandleSpotifyCallbackAsync(
    string code,
    IServiceProvider services,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(code))
    {
        return Results.BadRequest("Missing authorization code.");
    }

    var oauthService = services.GetService<SpotifyOAuthService>();
    var tokenStorage = services.GetService<ITokenStorage>();
    if (oauthService is null || tokenStorage is null)
    {
        return Results.BadRequest("Spotify OAuth is unavailable in mock mode.");
    }

    try
    {
        var result = await oauthService.ExchangeCodeForTokenAsync(code, cancellationToken);
        await tokenStorage.SaveTokensAsync(result.AccessToken, result.RefreshToken, result.ExpiresAt, cancellationToken);
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("SpotifyOAuth");
        logger.LogInformation("Spotify connected. Access token stored in memory until the process exits.");
        return Results.Content(
            "<html><body><p>Spotify connected, you can close this window.</p></body></html>",
            "text/html");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("SpotifyOAuth");
        logger.LogError(ex, "Failed to complete Spotify callback");
        return Results.Problem("Failed to complete Spotify OAuth callback.");
    }
}
