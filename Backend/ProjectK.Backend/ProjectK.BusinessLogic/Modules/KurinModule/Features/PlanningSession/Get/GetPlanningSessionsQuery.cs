using AutoMapper;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.PlanningSession.Get;

public record GetPlanningSessionsQuery(Guid kurinKey) : IRequest<ServiceResult<IEnumerable<PlanningSessionResponse>>>;

public class GetPlanningSessionsQueryHandler : IRequestHandler<GetPlanningSessionsQuery, ServiceResult<IEnumerable<PlanningSessionResponse>>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ICurrentUserContext _currentUser;

    public GetPlanningSessionsQueryHandler(IUnitOfWork uow, IMapper mapper, ICurrentUserContext currentUser)
    {
        _uow = uow;
        _mapper = mapper;
        _currentUser = currentUser;
    }
    public async Task<ServiceResult<IEnumerable<PlanningSessionResponse>>> Handle(GetPlanningSessionsQuery request, CancellationToken cancellationToken)
    {
        var entities = (await _uow.PlanningSessions.GetAllByKurinKeyAsync(request.kurinKey, cancellationToken)).ToList();
        var dtos = _mapper.Map<List<PlanningSessionResponse>>(entities);

        foreach (var (dto, entity) in dtos.Zip(entities))
        {
            dto.CanDelete = PlanningSessionAccess.CanDelete(_currentUser, entity.CreatedByUserKey);
        }

        return new ServiceResult<IEnumerable<PlanningSessionResponse>>(ResultType.Success, dtos);
    }
}
