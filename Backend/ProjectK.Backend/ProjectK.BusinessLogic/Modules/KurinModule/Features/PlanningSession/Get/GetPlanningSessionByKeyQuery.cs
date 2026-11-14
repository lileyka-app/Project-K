using AutoMapper;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.PlanningSession.Get;

public record GetPlanningSessionByKeyQuery(Guid entityKey) : IRequest<ServiceResult<PlanningSessionResponse>>;

public class GetPlanningSessionByKeyQueryHandler : IRequestHandler<GetPlanningSessionByKeyQuery, ServiceResult<PlanningSessionResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ICurrentUserContext _currentUser;

    public GetPlanningSessionByKeyQueryHandler(IUnitOfWork uow, IMapper mapper, ICurrentUserContext currentUser)
    {
        _uow = uow;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<PlanningSessionResponse>> Handle(GetPlanningSessionByKeyQuery request, CancellationToken cancellationToken)
    {
        var entity = await _uow.PlanningSessions.GetByKeyWithDetailsAsync(request.entityKey, cancellationToken);

        if (entity == null)
        {
            return new ServiceResult<PlanningSessionResponse>(ResultType.NotFound);
        }

        var dto = _mapper.Map<PlanningSessionResponse>(entity);
        dto.CanDelete = PlanningSessionAccess.CanDelete(_currentUser, entity.CreatedByUserKey);
        return new ServiceResult<PlanningSessionResponse>(ResultType.Success, dto);
    }
}
