namespace Core.Adapters;

public interface IGameAdapter<in TInput>
{
    string TitleId { get; }

    AdapterObservation Adapt(TInput payload, DateTimeOffset receivedAt);
}
