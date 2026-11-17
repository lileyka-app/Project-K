using MediatR;
using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Dtos.AgendaModule;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AgendaModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Features.Agenda;

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
    private readonly IAgendaViewerDirectory _scopes;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAgendaDirectory _agenda;

    public GetMyTasksQueryHandler(IAgendaViewerDirectory scopes, ICurrentUserContext currentUser, IAgendaDirectory agenda)
    {
        _scopes = scopes;
        _currentUser = currentUser;
        _agenda = agenda;
    }

    public async Task<ServiceResult<IReadOnlyList<MyTaskDto>>> Handle(GetMyTasksQuery request, CancellationToken cancellationToken)
    {
        var rows = new List<MyTaskDto>();

        foreach (var context in await _scopes.GetForCurrentAccountAsync(cancellationToken))
        {
            var kurin = MyKurinRefDto.Of(context.Membership, _currentUser.KurinKey);
            foreach (var item in await _agenda.GetTasksForViewerAsync(context.Viewer, cancellationToken))
            {
                if (item.ViewerStatus is not { } status || status == AgendaItemStatus.Done)
                {
                    continue;
                }

                rows.Add(ToRow(item, kurin, status));
            }
        }

        return new ServiceResult<IReadOnlyList<MyTaskDto>>(ResultType.Success, rows
            .OrderBy(r => r.EndUtc ?? r.StartUtc ?? DateTime.MaxValue)
            .ThenBy(r => r.Title)
            .ToList());
    }

    private static MyTaskDto ToRow(AgendaViewerItem item, MyKurinRefDto kurin, AgendaItemStatus status) => new()
    {
        AgendaItemKey = item.AgendaItemKey,
        Kurin = kurin,
        Title = item.Title,
        Status = status,
        StartUtc = item.StartUtc is { } start ? DateTime.SpecifyKind(start, DateTimeKind.Utc) : null,
        EndUtc = item.EndUtc is { } end ? DateTime.SpecifyKind(end, DateTimeKind.Utc) : null,
        CanChangeStatus = kurin.IsCurrent && item.CanChangeStatus
    };
}
