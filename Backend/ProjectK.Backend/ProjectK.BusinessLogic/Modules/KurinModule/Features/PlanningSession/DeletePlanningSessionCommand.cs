using MediatR;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.PlanningSession;

/// <summary>
/// Withdraws a planning session. Answers with a line of plain text, which both clients read as text
/// and ignore; it stays so the reply does not change under them.
/// </summary>
public record DeletePlanningSessionCommand(Guid planningSessionKey) : IRequest<ServiceResult<string>>;

public class DeletePlanningSessionCommandHandler : IRequestHandler<DeletePlanningSessionCommand, ServiceResult<string>>
{
    private readonly IUnitOfWork _uow;
    public DeletePlanningSessionCommandHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }
    public async Task<ServiceResult<string>> Handle(DeletePlanningSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _uow.PlanningSessions.GetByKeyAsync(request.planningSessionKey, cancellationToken);
        if (session == null)
        {
            return ServiceResult<string>.Failure(Common.Models.Enums.ResultType.NotFound, KurinErrors.PlanningSessionNotFound, "Planning session was not found.");
        }
        _uow.PlanningSessions.Delete(session, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return new ServiceResult<string>(Common.Models.Enums.ResultType.Success, "Planning deleted successfully");
    }
}
