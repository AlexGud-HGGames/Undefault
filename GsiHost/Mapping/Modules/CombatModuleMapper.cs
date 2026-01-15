using Core.Models;
using GsiHost.Dtos;

namespace GsiHost.Mapping.Modules;

public sealed class CombatModuleMapper : ISnapshotModuleMapper
{
    public ISnapshotModule? Map(GsiPayloadDto payload)
    {
        var activity = payload.Player?.Activity;
        var inCombat = activity?.Contains("combat", StringComparison.OrdinalIgnoreCase) == true;

        return new CombatModule(
            InCombatHint: inCombat,
            LastDamageDealtAt: null,
            LastDamageReceivedAt: null
        );
    }
}
