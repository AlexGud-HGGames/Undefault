using System;

namespace UI.Models;

public record UiLogEntry(
    DateTimeOffset Timestamp,
    string Category,
    string Message
);
