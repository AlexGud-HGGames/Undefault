using Core.Adapters;
using Core.Models;
using Core.Music;
using GsiHost.Dtos;
using GsiHost.Mapping;

namespace GsiHost.Adapters;

public sealed class Cs2GameAdapter : IGameAdapter<GsiPayloadDto>
{
    private readonly GsiSnapshotMapper _mapper;

    public Cs2GameAdapter(GsiSnapshotMapper mapper)
    {
        _mapper = mapper;
    }

    public string TitleId => "cs2";

    public AdapterObservation Adapt(GsiPayloadDto payload, DateTimeOffset receivedAt)
    {
        var snapshot = _mapper.Map(payload, receivedAt);
        var round = snapshot.GetModule<RoundModule>();
        var clock = new GameClockSnapshot(
            WallTimeUtc: receivedAt,
            GameTimeSeconds: null,
            IsGamePaused: false,
            MatchPhase: MatchPhaseNeutral.Unknown,
            RoundIndex: round?.Round);

        return new AdapterObservation(
            Raw: snapshot,
            Clock: clock,
            Neutral: NeutralContext.Unknown(receivedAt),
            DomainEvents: Array.Empty<TitleDomainEvent>(),
            Safety: SafetyFacts.Unknown("phase1-not-computed"));
    }
}
