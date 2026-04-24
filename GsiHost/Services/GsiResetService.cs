using Core.Rules;

namespace GsiHost.Services;

public sealed class GsiResetService : IGsiResetService
{
    private readonly IRulesEngine _rulesEngine;
    private readonly AppStateService _appState;

    public GsiResetService(IRulesEngine rulesEngine, AppStateService appState)
    {
        _rulesEngine = rulesEngine;
        _appState = appState;
    }

    public void Reset()
    {
        _rulesEngine.Reset();
        _appState.ClearRecentEvents();
    }
}
