using MediatR;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.IdentityModule;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.IdentityModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.Duties;

/// <summary>
/// What waits on the person as провід, in every kurin where they hold an office, collected from every
/// <see cref="IDutySource"/>: вмілості to confirm, money to acknowledge or verify, past events nobody
/// marked attendance at. Each source answers only what the person's offices there give the right to
/// act on, so the list never names a thing the page behind it would refuse. What the person hid
/// stays out while it has not grown, and is counted instead.
/// </summary>
public sealed record GetMyDutiesQuery : IRequest<ServiceResult<MyDutiesDto>>;

public sealed class GetMyDutiesQueryHandler : IRequestHandler<GetMyDutiesQuery, ServiceResult<MyDutiesDto>>
{
    private readonly IAgendaViewerDirectory _viewers;
    private readonly IEnumerable<IDutySource> _sources;
    private readonly IPermissionResolver _permissions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;

    public GetMyDutiesQueryHandler(
        IAgendaViewerDirectory viewers,
        IEnumerable<IDutySource> sources,
        IPermissionResolver permissions,
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser)
    {
        _viewers = viewers;
        _sources = sources;
        _permissions = permissions;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<MyDutiesDto>> Handle(GetMyDutiesQuery request, CancellationToken cancellationToken)
    {
        var duties = new List<MyDutyDto>();

        foreach (var context in await _viewers.GetForCurrentAccountAsync(cancellationToken))
        {
            if (context.Roles.Count == 0)
            {
                continue;
            }

            var kurinKey = context.Membership.KurinKey;
            var permissions = await _permissions.ResolveAsync(context.Roles, kurinKey, cancellationToken);
            var viewer = new DutyViewer(kurinKey, context.Viewer, new DutyRights(permissions, context.LedGroupKeys));
            var kurin = MyKurinRefDto.Of(context.Membership, _currentUser.KurinKey);

            var found = new List<PendingDuty>();
            foreach (var source in _sources)
            {
                found.AddRange(await source.GetDutiesAsync(viewer, cancellationToken));
            }

            // By kind, not by the order the sources happen to be registered in; OrderBy is stable, so a source's own order stays.
            duties.AddRange(found.OrderBy(duty => duty.Kind).Select(duty => ToDto(duty, kurin)));
        }

        var dismissed = _currentUser.UserId is { } userKey && duties.Count > 0
            ? (await _unitOfWork.DismissedDuties.GetForUserAsync(userKey, cancellationToken)).ToDictionary(d => d.DutyKey, StringComparer.Ordinal)
            : new Dictionary<string, DismissedDuty>(StringComparer.Ordinal);
        bool Hidden(MyDutyDto duty) => dismissed.TryGetValue(duty.Key, out var dismissal) && DutyDismissalRules.StillHidden(duty, dismissal);

        return new ServiceResult<MyDutiesDto>(ResultType.Success, new MyDutiesDto
        {
            Items = duties.Where(duty => !Hidden(duty)).ToList(),
            HiddenCount = duties.Count(Hidden)
        });
    }

    private static MyDutyDto ToDto(PendingDuty duty, MyKurinRefDto kurin)
    {
        var dto = new MyDutyDto
        {
            Kind = duty.Kind,
            Kurin = kurin,
            Count = duty.Count,
            GroupKey = duty.GroupKey,
            GroupName = duty.GroupName,
            AgendaItemKey = duty.AgendaItemKey,
            OccurrenceStartUtc = duty.OccurrenceStartUtc,
            Title = duty.Title,
            MemberKey = duty.MemberKey
        };
        return dto with { Key = DutyDismissalRules.KeyOf(dto), Fingerprint = DutyDismissalRules.FingerprintOf(dto) };
    }
}
