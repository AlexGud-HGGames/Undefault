using Core.Music;

namespace GsiHost.Services;

public interface IShadowMusicSnapshotSink
{
    void Record(MusicEngineDebugSnapshot snapshot);

    MusicEngineDebugSnapshot? Latest { get; }

    IReadOnlyList<MusicEngineDebugSnapshot> Recent();

    void Clear();
}
