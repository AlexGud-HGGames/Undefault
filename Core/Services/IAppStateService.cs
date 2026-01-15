using System;
using Core.Models;

namespace Core.Services;

public interface IAppStateService
{
    IObservable<StatusSnapshot> StatusStream { get; }
    IObservable<EventLogEntry> LogStream { get; }
}
