using Core.Rules;

namespace GsiHost.Services;

public sealed class GsiResetService : IGsiResetService
{
    private readonly IRulesEngine _rulesEngine;
    private readonly AppStateService _appState;
    private readonly TimelineCaptureService _timeline;
    private readonly IShadowMusicSnapshotSink _shadowSink;

    public GsiResetService(
        IRulesEngine rulesEngine,
        AppStateService appState,
        TimelineCaptureService timeline,
        IShadowMusicSnapshotSink shadowSink)
    {
        _rulesEngine = rulesEngine;
        _appState = appState;
        _timeline = timeline;
        _shadowSink = shadowSink;
    }

    public void Reset()
    {
        _rulesEngine.Reset();
        _appState.ClearRecentEvents();
        _timeline.Reset();
        _shadowSink.Clear();
    }
}
