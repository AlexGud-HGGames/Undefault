using Core.Actions;
using Core.Diff;
using Core.Models;
using Core.Rules;
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
builder.Services.AddSingleton<ISnapshotModuleMapper, VitalsModuleMapper>();
builder.Services.AddSingleton<ISnapshotModuleMapper, PositionModuleMapper>();
builder.Services.AddSingleton<ISnapshotModuleMapper, CombatModuleMapper>();

builder.Services.Configure<RulesEngineOptions>(options =>
{
    options.ActionMap[EventType.Death] = new() { "log" };
    options.ActionMap[EventType.Combat] = new() { "log" };
    options.ActionMap[EventType.Idle] = new() { "log" };
});

var app = builder.Build();

app.MapGet("/", () => "UndefaultIt GSI Host");

app.MapPost("/gsi", (GsiPayloadDto payload, GsiProcessingService processor) =>
{
    var events = processor.Process(payload);
    return Results.Ok(new { events = events.Count });
});

app.Run();
