using MediatR;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Dtos.UsersModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.TileLayout.Save;

public class SaveTileLayoutCommandHandler : IRequestHandler<SaveTileLayoutCommand, ServiceResult<TileLayoutDto>>
{
    private const int MaxOrderJsonLength = 2000;

    private readonly IUnitOfWork _unitOfWork;

    public SaveTileLayoutCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<TileLayoutDto>> Handle(SaveTileLayoutCommand request, CancellationToken cancellationToken)
    {
        var tileKeys = request.TileKeys ?? [];

        var hiddenKeys = request.HiddenTileKeys ?? [];

        var orderJson = TileOrderSerializer.Serialize(tileKeys);
        var hiddenJson = TileOrderSerializer.Serialize(hiddenKeys);
        if (orderJson.Length > MaxOrderJsonLength || hiddenJson.Length > MaxOrderJsonLength)
        {
            return ServiceResult<TileLayoutDto>.Failure(ResultType.BadRequest, "LayoutTooLarge", "Tile layout is too large.");
        }

        var schemaVersion = request.SchemaVersion <= 0 ? 1 : request.SchemaVersion;
        var existing = await _unitOfWork.UserTileLayouts.GetByBoardAsync(request.UserKey, request.BoardKey, cancellationToken);

        if (existing == null)
        {
            existing = new UserTileLayout
            {
                UserTileLayoutKey = Guid.NewGuid(),
                UserKey = request.UserKey,
                BoardKey = request.BoardKey,
                TileOrderJson = orderJson,
                HiddenTilesJson = hiddenJson,
                SchemaVersion = schemaVersion,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _unitOfWork.UserTileLayouts.Create(existing, cancellationToken);
        }
        else
        {
            existing.TileOrderJson = orderJson;
            existing.HiddenTilesJson = hiddenJson;
            existing.SchemaVersion = schemaVersion;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            _unitOfWork.UserTileLayouts.Update(existing, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new TileLayoutDto(existing.BoardKey, tileKeys, hiddenKeys, existing.SchemaVersion, existing.UpdatedAtUtc);
        return new ServiceResult<TileLayoutDto>(ResultType.Success, dto);
    }
}
