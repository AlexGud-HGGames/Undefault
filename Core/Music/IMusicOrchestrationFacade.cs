using Core.Adapters;

namespace Core.Music;

public interface IMusicOrchestrationFacade
{
    // Shadow mode: deterministic, side-effect free. No Spotify calls, no detector mutation.
    MusicEngineDebugSnapshot EvaluateShadow(AdapterObservation observation);
}
