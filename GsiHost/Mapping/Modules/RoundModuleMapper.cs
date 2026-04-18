using Core.Models;
using GsiHost.Dtos;

namespace GsiHost.Mapping.Modules;

public sealed class RoundModuleMapper : ISnapshotModuleMapper
{
    public ISnapshotModule? Map(GsiPayloadDto payload)
    {
        var map = payload.Map;
        if (map is null)
        {
            return null;
        }

        return new RoundModule(
            Round: map.Round,
            Phase: map.Phase);
    }
}
