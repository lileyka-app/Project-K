using MediatR;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.TileLayout;

public sealed record GetTileLayoutsQuery(Guid UserKey) : IRequest<ServiceResult<IReadOnlyList<TileLayoutDto>>>;

public sealed class GetTileLayoutsQueryHandler : IRequestHandler<GetTileLayoutsQuery, ServiceResult<IReadOnlyList<TileLayoutDto>>>
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
            .Where(layout => TileBoardNames.All.Contains(layout.BoardKey))
            .Select(layout => layout.ToDto())
            .ToList();

        return new ServiceResult<IReadOnlyList<TileLayoutDto>>(ResultType.Success, dtos);
    }
}
