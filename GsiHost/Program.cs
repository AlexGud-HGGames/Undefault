using System.Threading;
using Core.Actions;
using Core.Actions.Spotify;
using Core.Diff;
using Core.Models;
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
builder.Services.AddSingleton<IRulesEngine, RulesEngine>();
builder.Services.AddSingleton<GsiProcessingService>();
builder.Services.AddSingleton<AppStateService>();
builder.Services.AddSingleton<IAppStateService>(sp => sp.GetRequiredService<AppStateService>());
builder.Services.AddSingleton<ISnapshotModuleMapper, VitalsModuleMapper>();
builder.Services.AddSingleton<ISnapshotModuleMapper, PositionModuleMapper>();
builder.Services.AddSingleton<ISnapshotModuleMapper, CombatModuleMapper>();

BuildSpotify(builder);

builder.Services.Configure<RulesEngineOptions>(
    builder.Configuration.GetSection("RulesEngine"));
builder.Services.Configure<SpotifyClientOptions>(
    builder.Configuration.GetSection("Spotify"));
builder.Services.Configure<SpotifyActionOptions>(
    builder.Configuration.GetSection("SpotifyActions"));

var app = builder.Build();

app.MapGet("/", () => "UndefaultIt GSI Host");

app.MapPost("/gsi", (GsiPayloadDto payload, GsiProcessingService processor) =>
{
    var events = processor.Process(payload);
    return Results.Ok(new { events = events.Count });
});

app.MapGet("/status", async (IAppStateService appStateService, CancellationToken cancellationToken) =>
{
    var status = await appStateService.GetCurrentStatusAsync(cancellationToken);
    return Results.Ok(status);
});

app.MapGet("/events", (AppStateService appStateService) =>
{
    return Results.Ok(appStateService.GetRecentEvents());
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

    // Spotify actions
    webApplicationBuilder.Services.AddSingleton<IEventAction, SpotifyPauseAction>();
    webApplicationBuilder.Services.AddSingleton<IEventAction, SpotifyPlayAction>();
    webApplicationBuilder.Services.AddSingleton<IEventAction, SpotifyResumeAction>();
    webApplicationBuilder.Services.AddSingleton<IEventAction, SpotifySetVolumeAction>();
}
