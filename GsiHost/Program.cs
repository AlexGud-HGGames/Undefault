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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<GsiSnapshotMapper>();
builder.Services.AddSingleton<SnapshotDiffer>();
builder.Services.AddSingleton<EventDetector>();
builder.Services.AddSingleton<ISnapshotStore, InMemorySnapshotStore>();
builder.Services.AddSingleton<IEventAction, LogEventAction>();
builder.Services.AddSingleton<IEventAction, SpotifyProfileAction>();
builder.Services.AddSingleton<IRulesEngine, RulesEngine>();
builder.Services.AddSingleton<GsiProcessingService>();
builder.Services.AddSingleton<AppStateService>();
builder.Services.AddSingleton<IAppStateService>(sp => sp.GetRequiredService<AppStateService>());
builder.Services.AddSingleton<IConfigurationService, AppSettingsConfigurationService>();
builder.Services.AddSingleton<IProfileService, JsonProfileService>();
builder.Services.AddSingleton<ISnapshotModuleMapper, VitalsModuleMapper>();
builder.Services.AddSingleton<ISnapshotModuleMapper, PositionModuleMapper>();
builder.Services.AddSingleton<ISnapshotModuleMapper, CombatModuleMapper>();

BuildSpotify(builder);

builder.Services.Configure<RulesEngineOptions>(
    builder.Configuration.GetSection("RulesEngine"));
builder.Services.Configure<SpotifyClientOptions>(
    builder.Configuration.GetSection("Spotify"));

var app = builder.Build();

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

app.MapGet("/spotify/authorize", (SpotifyOAuthService oauthService) =>
{
    var state = Guid.NewGuid().ToString("N");
    var url = oauthService.GetAuthorizationUrl(state);
    return Results.Ok(new { url, state });
});

app.MapGet("/spotify/callback", async (
    string code,
    SpotifyOAuthService oauthService,
    ITokenStorage tokenStorage,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(code))
    {
        return Results.BadRequest("Missing authorization code.");
    }

    var result = await oauthService.ExchangeCodeForTokenAsync(code, cancellationToken);
    await tokenStorage.SaveTokensAsync(result.AccessToken, result.RefreshToken, result.ExpiresAt, cancellationToken);
    return Results.Ok("Spotify authorized. You can close this window.");
});

app.Run();

void BuildSpotify(WebApplicationBuilder webApplicationBuilder)
{
    // Spotify services
    webApplicationBuilder.Services.AddHttpClient("SpotifyApi");
    webApplicationBuilder.Services.AddHttpClient("SpotifyOAuth");
    webApplicationBuilder.Services.AddSingleton<ITokenStorage, InMemoryTokenStorage>();
    webApplicationBuilder.Services.AddSingleton<SpotifyOAuthService>();
    webApplicationBuilder.Services.AddSingleton<ISpotifyClient, SpotifyClient>();

}
