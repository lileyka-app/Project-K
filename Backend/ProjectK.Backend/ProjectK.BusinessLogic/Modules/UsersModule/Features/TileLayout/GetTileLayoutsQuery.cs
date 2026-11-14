using MediatR;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.UsersModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Rules.UsersModule;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.TileLayout;

public record GetTileLayoutsQuery(Guid UserKey) : IRequest<ServiceResult<IReadOnlyList<TileLayoutDto>>>;

public class GetTileLayoutsQueryHandler : IRequestHandler<GetTileLayoutsQuery, ServiceResult<IReadOnlyList<TileLayoutDto>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetTileLayoutsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<IReadOnlyList<TileLayoutDto>>> Handle(GetTileLayoutsQuery request, CancellationToken cancellationToken)
    {
        var layouts = await _unitOfWork.UserTileLayouts.GetByUserAsync(request.UserKey, cancellationToken);

        var dtos = layouts
            .Where(layout => TileBoardKeys.All.Contains(layout.BoardKey))
            .Select(layout => layout.ToDto())
            .ToList();

        return new ServiceResult<IReadOnlyList<TileLayoutDto>>(ResultType.Success, dtos);
    }
}
