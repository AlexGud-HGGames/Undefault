using System;
using Core.Models;

namespace UI.Models;

public sealed record LogEntry(
    EventType EventType,
    DateTimeOffset Timestamp,
    string Message
);
