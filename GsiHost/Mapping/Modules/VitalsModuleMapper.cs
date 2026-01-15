using Core.Models;
using GsiHost.Dtos;

namespace GsiHost.Mapping.Modules;

public sealed class VitalsModuleMapper : ISnapshotModuleMapper
{
    public ISnapshotModule? Map(GsiPayloadDto payload)
    {
        var state = payload.Player?.State;
        if (state is null)
        {
            return null;
        }

        var health = state.Health ?? 0;
        var armor = state.Armor ?? 0;

        return new VitalsModule(
            Health: health,
            Armor: armor,
            IsAlive: health > 0
        );
    }
}
