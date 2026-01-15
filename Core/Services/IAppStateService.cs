using System;
using Core.Models;

namespace Core.Services;

public interface IAppStateService
{
    IObservable<StatusSnapshot> StatusSnapshot { get; }
    IObservable<NormalizedEvent> Events { get; }
    Task<StatusSnapshot> GetCurrentStatusAsync(CancellationToken cancellationToken = default);
}
