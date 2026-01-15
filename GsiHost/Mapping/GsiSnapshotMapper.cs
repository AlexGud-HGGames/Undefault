using System.Globalization;
using System.Linq;
using Core.Models;
using GsiHost.Dtos;

namespace GsiHost.Mapping;

public sealed class GsiSnapshotMapper
{
    private readonly IReadOnlyList<ISnapshotModuleMapper> _moduleMappers;

    public GsiSnapshotMapper(IEnumerable<ISnapshotModuleMapper> moduleMappers)
    {
        _moduleMappers = moduleMappers.ToList();
    }

    public GameSnapshot Map(GsiPayloadDto payload, DateTimeOffset receivedAt)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        var timestamp = ResolveTimestamp(payload, receivedAt);
        var player = payload.Player;
        var modules = _moduleMappers
            .Select(mapper => mapper.Map(payload))
            .Where(module => module is not null)
            .Cast<ISnapshotModule>()
            .ToList();

        return new GameSnapshot(
            Timestamp: timestamp,
            GameId: payload.Provider?.Name ?? payload.Provider?.AppId?.ToString(CultureInfo.InvariantCulture),
            MatchId: payload.Map?.MatchId ?? payload.Map?.Name,
            PlayerId: player?.SteamId,
            Modules: modules
        );
    }

    private static DateTimeOffset ResolveTimestamp(GsiPayloadDto payload, DateTimeOffset receivedAt)
    {
        var unixSeconds = payload.Provider?.Timestamp;
        if (unixSeconds.HasValue && unixSeconds.Value > 0)
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value);
        }

        return receivedAt;
    }
}
