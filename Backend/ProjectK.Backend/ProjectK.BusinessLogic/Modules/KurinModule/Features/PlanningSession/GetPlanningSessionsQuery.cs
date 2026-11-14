using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.PlanningSession;

public record GetPlanningSessionsQuery(Guid kurinKey) : IRequest<ServiceResult<IEnumerable<PlanningSessionDto>>>;

public class GetPlanningSessionsQueryHandler : IRequestHandler<GetPlanningSessionsQuery, ServiceResult<IEnumerable<PlanningSessionDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _currentUser;

    public GetPlanningSessionsQueryHandler(IUnitOfWork uow, ICurrentUserContext currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }
    public async Task<ServiceResult<IEnumerable<PlanningSessionDto>>> Handle(GetPlanningSessionsQuery request, CancellationToken cancellationToken)
    {
        var entities = (await _uow.PlanningSessions.GetAllByKurinKeyAsync(request.kurinKey, cancellationToken)).ToList();
        var dtos = entities
            .Select(entity => entity.ToDto(PlanningSessionAccess.CanDelete(_currentUser, entity.CreatedByUserKey)))
            .ToList();

        return new ServiceResult<IEnumerable<PlanningSessionDto>>(ResultType.Success, dtos);
    }
}
