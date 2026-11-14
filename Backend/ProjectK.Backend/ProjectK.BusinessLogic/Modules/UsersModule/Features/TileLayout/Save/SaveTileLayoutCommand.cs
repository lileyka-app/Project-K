using MediatR;
using ProjectK.Common.Models.Dtos.UsersModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.TileLayout.Save;

public record SaveTileLayoutCommand(
    Guid UserKey,
    string BoardKey,
    IReadOnlyList<string> TileKeys,
    IReadOnlyList<string> HiddenTileKeys,
    int SchemaVersion) : IRequest<ServiceResult<TileLayoutDto>>;
