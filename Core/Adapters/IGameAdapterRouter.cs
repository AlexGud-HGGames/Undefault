namespace Core.Adapters;

/// <summary>
/// Read-only registry of <see cref="GameAdapterRegistration"/> entries the host serves.
/// Lookup is metadata-only; the host wires endpoints per title with their own typed
/// payload DTOs and does not deserialize raw payloads through this interface.
/// </summary>
public interface IGameAdapterRouter
{
    IReadOnlyList<GameAdapterRegistration> Registrations { get; }

    bool TryResolveByPath(string endpointPath, out GameAdapterRegistration registration);

    bool TryResolveByAppId(int appId, out GameAdapterRegistration registration);
}
