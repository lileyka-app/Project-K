using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.PlanningSession;

public record GetPlanningSessionByKeyQuery(Guid entityKey) : IRequest<ServiceResult<PlanningSessionDto>>;

public class GetPlanningSessionByKeyQueryHandler : IRequestHandler<GetPlanningSessionByKeyQuery, ServiceResult<PlanningSessionDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _currentUser;

    public GetPlanningSessionByKeyQueryHandler(IUnitOfWork uow, ICurrentUserContext currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<PlanningSessionDto>> Handle(GetPlanningSessionByKeyQuery request, CancellationToken cancellationToken)
    {
        var entity = await _uow.PlanningSessions.GetByKeyWithDetailsAsync(request.entityKey, cancellationToken);

        if (entity == null)
        {
            return ServiceResult<PlanningSessionDto>.Failure(ResultType.NotFound, KurinErrors.PlanningSessionNotFound, "Planning session was not found.");
        }

        var dto = entity.ToDto(PlanningSessionAccess.CanDelete(_currentUser, entity.CreatedByUserKey));
        return new ServiceResult<PlanningSessionDto>(ResultType.Success, dto);
    }
}
