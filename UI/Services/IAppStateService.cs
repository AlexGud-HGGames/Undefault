using System;
using UI.Models;

namespace UI.Services;

public interface IAppStateService
{
    IObservable<UiStatusSnapshot> StatusStream { get; }
}
