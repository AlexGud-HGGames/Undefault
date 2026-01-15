using System;
using UI.Models;

namespace UI.Services;

public interface ILogStreamService
{
    IObservable<UiLogEntry> LogEntries { get; }
}
