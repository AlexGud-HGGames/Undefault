using Core.Models;

namespace GsiHost.Dtos;

public sealed record UserActionRequest(
    string EventKey,
    string? Action = null,
    string? Detail = null);

public sealed record UserActionResponse(
    TimelineEntry Entry,
    TimelineCommandOutcome Outcome);
