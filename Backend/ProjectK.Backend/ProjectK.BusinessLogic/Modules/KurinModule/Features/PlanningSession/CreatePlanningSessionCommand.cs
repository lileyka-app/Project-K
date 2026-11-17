using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule.Requests;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Optimization.Abstractions;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.PlanningSession;

public sealed class CreatePlanningSessionCommand : IRequest<ServiceResult<Guid>>
{
    public string Name { get; set; } = string.Empty;
    public Guid KurinKey { get; set; }
    public DateTime SearchStart { get; set; }
    public DateTime SearchEnd { get; set; }
    public int DurationDays { get; set; }
    public List<ParticipantInput> Participants { get; set; } = [];
}

public sealed class CreatePlanningSessionCommandHandler : IRequestHandler<CreatePlanningSessionCommand, ServiceResult<Guid>>
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

        var problem = new CampDateProblem(
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
