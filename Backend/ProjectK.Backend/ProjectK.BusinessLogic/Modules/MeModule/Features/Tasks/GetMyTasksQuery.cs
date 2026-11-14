using MediatR;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MeModule;
using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AgendaModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Tasks;

/// <summary>
/// The person's open tasks across their kurins — only where the next move is theirs: a task handed to
/// them, their part of one done «кожному окремо», a shared one anyone in their гурток may close, one
/// their office answers for. Not the ones they only set for others, nor one their гурток's провід is
/// to close: those they follow on the board, there is nothing here for them to do. A status is movable from here only
/// in the kurin the token acts in — the board's own endpoint does the moving, for that kurin alone.
/// </summary>
public sealed record GetMyTasksQuery : IRequest<ServiceResult<IReadOnlyList<MyTaskDto>>>;

public sealed class GetMyTasksQueryHandler : IRequestHandler<GetMyTasksQuery, ServiceResult<IReadOnlyList<MyTaskDto>>>
{
    private readonly MeAgendaScopes _scopes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAgendaDirectory _agenda;

    public GetMyTasksQueryHandler(MeAgendaScopes scopes, IUnitOfWork unitOfWork, ICurrentUserContext currentUser, IAgendaDirectory agenda)
    {
        _scopes = scopes;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _agenda = agenda;
    }

    public async Task<ServiceResult<IReadOnlyList<MyTaskDto>>> Handle(GetMyTasksQuery request, CancellationToken cancellationToken)
    {
        var rows = new List<MyTaskDto>();

        foreach (var context in await _scopes.BuildAsync(cancellationToken))
        {
            var kurin = MeKurinMapping.Ref(context.Membership, _currentUser.KurinKey);
            var scope = context.Viewer.ToScope();
            var items = (await _unitOfWork.AgendaItems.GetForViewerAsync(scope, null, null, onlyDated: false, kind: AgendaItemKind.Task, cancellationToken)).ToList();
            var roster = await _agenda.GetRosterAsync(context.Membership.KurinKey, items, cancellationToken);
            var viewer = context.Viewer;

            foreach (var item in items)
            {
                if (AgendaCompletionRules.StakeOf(item, viewer) is null)
                {
                    continue;
                }

                var status = AgendaCompletionRules.ViewerStatus(item, viewer, roster);
                if (status == AgendaItemStatus.Done)
                {
                    continue;
                }

                rows.Add(ToRow(item, kurin, status, viewer));
            }
        }

        return new ServiceResult<IReadOnlyList<MyTaskDto>>(ResultType.Success, rows
            .OrderBy(r => r.EndUtc ?? r.StartUtc ?? DateTime.MaxValue)
            .ThenBy(r => r.Title)
            .ToList());
    }

    private static MyTaskDto ToRow(AgendaItem item, MyKurinRefDto kurin, AgendaItemStatus status, AgendaViewerContext viewer) => new()
    {
        AgendaItemKey = item.AgendaItemKey,
        Kurin = kurin,
        Title = item.Title,
        Status = status,
        StartUtc = item.StartUtc is { } start ? DateTime.SpecifyKind(start, DateTimeKind.Utc) : null,
        EndUtc = item.EndUtc is { } end ? DateTime.SpecifyKind(end, DateTimeKind.Utc) : null,
        CanChangeStatus = kurin.IsCurrent && AgendaCompletionRules.CanChangeStatus(item, viewer)
    };
}
