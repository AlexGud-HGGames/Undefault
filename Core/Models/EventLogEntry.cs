using System;

namespace Core.Models;

public sealed record EventLogEntry(
    DateTimeOffset Timestamp,
    string Category,
    string Message,
    string? Detail
);
