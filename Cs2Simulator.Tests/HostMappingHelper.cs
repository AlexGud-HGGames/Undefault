using System.Text.Json;
using Core.Models;
using Cs2Simulator.Scenarios.Json;
using Cs2Simulator.Scenarios.Models;
using GsiHost.Dtos;
using GsiHost.Mapping;
using GsiHost.Mapping.Modules;

namespace Cs2Simulator.Tests;

internal static class HostMappingHelper
{
    public static GsiSnapshotMapper CreateMapper()
    {
        return new GsiSnapshotMapper(new ISnapshotModuleMapper[]
        {
            new RoundModuleMapper(),
            new VitalsModuleMapper(),
            new PositionModuleMapper(),
            new CombatModuleMapper()
        });
    }

    public static GameSnapshot RoundTrip(Cs2Payload payload)
    {
        var json = Cs2PayloadJson.Serialize(payload);
        var dto = JsonSerializer.Deserialize<GsiPayloadDto>(json)
            ?? throw new InvalidOperationException("Failed to deserialize Cs2Payload into GsiPayloadDto.");
        return CreateMapper().Map(dto, DateTimeOffset.UnixEpoch);
    }
}
