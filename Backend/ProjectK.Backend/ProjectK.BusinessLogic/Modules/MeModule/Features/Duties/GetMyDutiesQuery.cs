using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Modules.MeModule.Models;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Duties;

/// <summary>
/// What waits on the person as провід, in every kurin where they hold an office: вмілості to
/// confirm, money to acknowledge or verify, past events nobody marked attendance at. Each source is
/// read only where their offices there give the right to act on it, so the list never names a thing
/// the page behind it would refuse.
/// </summary>
public sealed record GetMyDutiesQuery : IRequest<ServiceResult<IReadOnlyList<MyDutyDto>>>;

public sealed class GetMyDutiesQueryHandler : IRequestHandler<GetMyDutiesQuery, ServiceResult<IReadOnlyList<MyDutyDto>>>
{
    private const int AttendanceWindowDays = 14;

    private readonly MeAgendaScopes _scopes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDuesUnitOfWork _dues;
    private readonly IMemberDirectory _members;
    private readonly IMemberProgressDirectory _progress;
    private readonly ScoreBookReader _books;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;
    private readonly IKraiProfileProvider _profiles;

    public GetMyDutiesQueryHandler(
        MeAgendaScopes scopes,
        IUnitOfWork unitOfWork,
        IDuesUnitOfWork dues,
        IMemberDirectory members,
        IMemberProgressDirectory progress,
        ScoreBookReader books,
        ICurrentUserContext currentUser,
        TimeProvider time,
        IKraiProfileProvider profiles)
    {
        _scopes = scopes;
        _unitOfWork = unitOfWork;
        _dues = dues;
        _members = members;
        _progress = progress;
        _books = books;
        _currentUser = currentUser;
        _time = time;
        _profiles = profiles;
    }

    public async Task<ServiceResult<IReadOnlyList<MyDutyDto>>> Handle(GetMyDutiesQuery request, CancellationToken cancellationToken)
    {
        var duties = new List<MyDutyDto>();

        foreach (var context in await _scopes.BuildAsync(cancellationToken))
        {
            if (context.Roles.Count == 0)
            {
                continue;
            }

            var rights = new DutyRights(context.Roles, context.LedGroupKeys);
            var kurin = MeKurins.Ref(context.Membership, _currentUser.KurinKey);

            await AddBadgesAsync(duties, context, rights, kurin, cancellationToken);
            await AddDuesAsync(duties, context, rights, kurin, cancellationToken);
            await AddAttendanceAsync(duties, context, rights, kurin, cancellationToken);
        }

        return new ServiceResult<IReadOnlyList<MyDutyDto>>(ResultType.Success, duties);
    }

    /// <summary>Вмілості handed in by the youths whose progress the person may confirm.</summary>
    private async Task AddBadgesAsync(List<MyDutyDto> duties, MeKurinContext context, DutyRights rights, MyKurinRefDto kurin, CancellationToken cancellationToken)
    {
        if (!rights.ForAnyGroup(ResourceType.BadgeProgress, ResourceAction.Update))
        {
            return;
        }

        var people = (await _members.GetByKurinAsync(context.Membership.KurinKey, cancellationToken))
            .Where(p => rights.ForGroup(ResourceType.BadgeProgress, ResourceAction.Update, p.GroupKey))
            .Select(p => p.MemberKey)
            .ToList();
        var count = await _progress.CountSubmittedBadgesAsync(people, cancellationToken);
        if (count > 0)
        {
            duties.Add(new MyDutyDto { Kind = MyDutyKind.BadgesToReview, Kurin = kurin, Count = count });
        }
    }

    /// <summary>
    /// Money waiting on a скарбник: transfers the kurin's one has not acknowledged, and entries in
    /// each box — a гурток's, or the kurin's own — that nobody has verified.
    /// </summary>
    private async Task AddDuesAsync(
        List<MyDutyDto> duties, MeKurinContext context, DutyRights rights, MyKurinRefDto kurin, CancellationToken cancellationToken)
    {
        var keepsKurinBox = rights.WholeKurin(ResourceType.KurinDues, ResourceAction.Update);
        if (!keepsKurinBox && !rights.ForAnyGroup(ResourceType.GroupDues, ResourceAction.Update))
        {
            return;
        }

        var entries = await _dues.DuesEntries.GetForKurinAsync(context.Membership.KurinKey, cancellationToken);

        if (keepsKurinBox)
        {
            var transfers = entries.Count(e => e.GroupKey.HasValue && e.Kind == DuesEntryKind.TransferToKurin && e.ReceivedAtUtc is null);
            if (transfers > 0)
            {
                duties.Add(new MyDutyDto { Kind = MyDutyKind.TransfersToConfirm, Kurin = kurin, Count = transfers });
            }

            var own = entries.Count(e => e.GroupKey is null && !e.IsVerified);
            if (own > 0)
            {
                duties.Add(new MyDutyDto { Kind = MyDutyKind.EntriesToVerify, Kurin = kurin, Count = own });
            }
        }

        var unverified = entries
            .Where(e => e.GroupKey.HasValue && !e.IsVerified)
            .GroupBy(e => e.GroupKey!.Value)
            .Where(g => rights.ForGroup(ResourceType.GroupDues, ResourceAction.Update, g.Key))
            .ToList();
        if (unverified.Count == 0)
        {
            return;
        }

        var groupNames = (await _unitOfWork.Groups.GetAllAsync(context.Membership.KurinKey, cancellationToken))
            .ToDictionary(g => g.GroupKey, g => g.Name);
        foreach (var group in unverified)
        {
            duties.Add(new MyDutyDto
            {
                Kind = MyDutyKind.EntriesToVerify,
                Kurin = kurin,
                Count = group.Count(),
                GroupKey = group.Key,
                GroupName = groupNames.GetValueOrDefault(group.Key, "—")
            });
        }
    }

    /// <summary>
    /// Events of the last two weeks worth points where no one at all was marked present — the sheet
    /// nobody opened. Only for someone who scores at least one гурток there.
    /// </summary>
    private async Task AddAttendanceAsync(List<MyDutyDto> duties, MeKurinContext context, DutyRights rights, MyKurinRefDto kurin, CancellationToken cancellationToken)
    {
        if (!rights.ForAnyGroup(ResourceType.GroupScore, ResourceAction.Create))
        {
            return;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var from = now.AddDays(-AttendanceWindowDays);
        var book = await _books.OpenAttendanceAsync(context.Membership.KurinKey, cancellationToken);
        var zone = await _profiles.ForKurinAsync(context.Membership.KurinKey, cancellationToken);
        var marked = book.Attendances
            .Where(a => a.RemovedAtUtc is null)
            .Select(a => (a.AgendaItemKey, a.OccurrenceStartUtc))
            .ToHashSet();

        var items = await _unitOfWork.AgendaItems.GetForViewerAsync(
            context.Viewer.ToScope(), from, now, onlyDated: true, kind: AgendaItemKind.Event, cancellationToken);

        foreach (var item in items)
        {
            if (book.AttendancePoints(item.AgendaItemKey, item.AgendaCategoryKey) <= 0)
            {
                continue;
            }

            foreach (var occurrence in AgendaRecurrence.Expand(item, from, now, zone))
            {
                if (occurrence.StartUtc > now || marked.Contains((item.AgendaItemKey, occurrence.StartUtc)))
                {
                    continue;
                }

                duties.Add(new MyDutyDto
                {
                    Kind = MyDutyKind.EventWithoutAttendance,
                    Kurin = kurin,
                    Count = 1,
                    AgendaItemKey = item.AgendaItemKey,
                    OccurrenceStartUtc = DateTime.SpecifyKind(occurrence.StartUtc, DateTimeKind.Utc),
                    Title = item.Title
                });
            }
        }
    }
}
