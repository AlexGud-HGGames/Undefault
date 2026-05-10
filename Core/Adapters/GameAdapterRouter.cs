namespace Core.Adapters;

public sealed class GameAdapterRouter : IGameAdapterRouter
{
    private readonly Dictionary<string, GameAdapterRegistration> _byPath;
    private readonly Dictionary<int, GameAdapterRegistration> _byAppId;

    public GameAdapterRouter(IEnumerable<GameAdapterRegistration> registrations)
    {
        if (registrations is null)
        {
            throw new ArgumentNullException(nameof(registrations));
        }

        var list = new List<GameAdapterRegistration>();
        _byPath = new Dictionary<string, GameAdapterRegistration>(StringComparer.OrdinalIgnoreCase);
        _byAppId = new Dictionary<int, GameAdapterRegistration>();

        foreach (var registration in registrations)
        {
            if (registration is null)
            {
                throw new ArgumentException("Registration entries must not be null.", nameof(registrations));
            }

            if (string.IsNullOrWhiteSpace(registration.TitleId))
            {
                throw new ArgumentException("Registration TitleId must be non-empty.", nameof(registrations));
            }

            if (string.IsNullOrWhiteSpace(registration.EndpointPath))
            {
                throw new ArgumentException(
                    $"Registration EndpointPath must be non-empty (titleId={registration.TitleId}).",
                    nameof(registrations));
            }

            if (_byPath.ContainsKey(registration.EndpointPath))
            {
                throw new ArgumentException(
                    $"Duplicate game adapter endpoint path '{registration.EndpointPath}'.",
                    nameof(registrations));
            }

            if (registration.AppId is int appId && _byAppId.ContainsKey(appId))
            {
                throw new ArgumentException(
                    $"Duplicate game adapter AppId {appId}.",
                    nameof(registrations));
            }

            _byPath[registration.EndpointPath] = registration;
            if (registration.AppId is int id)
            {
                _byAppId[id] = registration;
            }
            list.Add(registration);
        }

        Registrations = list;
    }

    public IReadOnlyList<GameAdapterRegistration> Registrations { get; }

    public bool TryResolveByPath(string endpointPath, out GameAdapterRegistration registration)
    {
        if (string.IsNullOrWhiteSpace(endpointPath))
        {
            registration = null!;
            return false;
        }

        return _byPath.TryGetValue(endpointPath, out registration!);
    }

    public bool TryResolveByAppId(int appId, out GameAdapterRegistration registration)
    {
        return _byAppId.TryGetValue(appId, out registration!);
    }
}
