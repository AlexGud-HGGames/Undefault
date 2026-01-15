using Core.Models;
using GsiHost.Dtos;

namespace GsiHost.Mapping;

public interface ISnapshotModuleMapper
{
    ISnapshotModule? Map(GsiPayloadDto payload);
}
