using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Optimization.Abstractions;
using PlanningSessionEntity = ProjectK.Common.Entities.KurinModule.Planning.PlanningSession;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.PlanningSession;

public class CreatePlanningSessionCommand : IRequest<ServiceResult<Guid>>
{
    public string Name { get; set; } = string.Empty;
    public Guid KurinKey { get; set; }
    public DateTime SearchStart { get; set; }
    public DateTime SearchEnd { get; set; }
    public int DurationDays { get; set; }
    public List<ParticipantInputDto> Participants { get; set; } = [];
}

public class CreatePlanningSessionCommandHandler : IRequestHandler<CreatePlanningSessionCommand, ServiceResult<Guid>>
{
    private readonly IUnitOfWork _uow;
    private readonly IOptimizer _optimizer;
    private readonly ICurrentUserContext _currentUserContext;
    public CreatePlanningSessionCommandHandler(IUnitOfWork uow, IOptimizer optimizer, ICurrentUserContext currentUserContext)
    {
        _uow = uow;
        _optimizer = optimizer;
        _currentUserContext = currentUserContext;
    }

    public async Task<ServiceResult<Guid>> Handle(CreatePlanningSessionCommand request, CancellationToken cancellationToken)
    {
        var session = request.ToEntity();
        session.CreatedByUserKey = _currentUserContext.UserId;

        var problem = new CampDateSolver(
            session.SearchStart,
            session.SearchEnd,
            session.DurationDays,
            session.Participants.ToList()
        );

        var result = _optimizer.Solve(problem, wolves: 40, iterations: 100);

        var bestStartDate = problem.PositionToDate(result.BestPosition);

        session.OptimalStartDate = bestStartDate;
        session.OptimalEndDate = bestStartDate.AddDays(session.DurationDays);
        session.ConflictScore = result.BestFitness;
        session.IsCalculated = true;

        _uow.PlanningSessions.Create(session, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);

        return new ServiceResult<Guid>(ResultType.Success, session.PlanningSessionKey);
    }
}
