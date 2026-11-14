using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.PlanningSession.Delete;

public record DeletePlanningSessionCommand(Guid planningSessionKey) : IRequest<ServiceResult<object>>;


public class DeletePlanningSessionCommandHandler : IRequestHandler<DeletePlanningSessionCommand, ServiceResult<object>>
{
    private readonly IUnitOfWork _uow;
    public DeletePlanningSessionCommandHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }
    public async Task<ServiceResult<object>> Handle(DeletePlanningSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _uow.PlanningSessions.GetByKeyAsync(request.planningSessionKey, cancellationToken);
        if (session == null)
        {
            return new ServiceResult<object>(Common.Models.Enums.ResultType.NotFound, "Planning session not found.");
        }
        _uow.PlanningSessions.Delete(session, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return new ServiceResult<object>(Common.Models.Enums.ResultType.Success, "Planning deleted successfully");
    }
}
