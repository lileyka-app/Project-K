using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Modules.ScoreModule.Models;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Sheet.Get;

/// <summary>
/// The sheet for one occurrence of an event: every current youth of the kurin, with their answer to
/// the invitation, whether the event was aimed at them, whether they are marked present and what
/// they were given here. The screen sorts them; the read only says what is true of each.
/// </summary>
public sealed record GetAttendanceSheetQuery(Guid KurinKey, Guid AgendaItemKey, DateTime OccurrenceStartUtc)
    : IRequest<ServiceResult<AttendanceSheetResponse>>;

public sealed class GetAttendanceSheetQueryHandler : IRequestHandler<GetAttendanceSheetQuery, ServiceResult<AttendanceSheetResponse>>
{
    private readonly ScoreAccess _access;
    private readonly ScoreBookReader _books;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKraiProfileProvider _profiles;

    public GetAttendanceSheetQueryHandler(ScoreAccess access, ScoreBookReader books, IUnitOfWork unitOfWork, IKraiProfileProvider profiles)
    {
        _access = access;
        _books = books;
        _unitOfWork = unitOfWork;
        _profiles = profiles;
    }

    public async Task<ServiceResult<AttendanceSheetResponse>> Handle(GetAttendanceSheetQuery request, CancellationToken cancellationToken)
    {
        if (_access.Refuse<AttendanceSheetResponse>(request.KurinKey) is { } refused)
        {
            return refused;
        }

        var occurrence = await EventOccurrence.LoadAsync(_unitOfWork, _profiles, request.KurinKey, request.AgendaItemKey, request.OccurrenceStartUtc, cancellationToken);
        if (occurrence is null)
        {
            return ScoreAccess.NotFound<AttendanceSheetResponse>("There is no such event, or it does not meet that day.");
        }

        var book = await _books.OpenAsync(request.KurinKey, cancellationToken);
        var item = occurrence.Item;

        var canScoreIn = await CanScoreByGroupAsync(request.KurinKey, book.GroupNames.Keys, cancellationToken);
        var canScoreUnplaced = await _access.MayScoreAsync(request.KurinKey, null, ResourceAction.Create, cancellationToken);
        bool CanScore(Guid? groupKey) => groupKey is { } key ? canScoreIn.GetValueOrDefault(key) : canScoreUnplaced;

        if (!canScoreUnplaced && !canScoreIn.Values.Any(v => v))
        {
            return ScoreAccess.Forbidden<AttendanceSheetResponse>();
        }

        // The answers of this very day: a series' RSVPs are per occurrence, a one-off's sit under null.
        var responses = (await _unitOfWork.AgendaResponses.GetForItemAsync(item.AgendaItemKey, AgendaOccurrences.KeyOf(item, occurrence.StartUtc), cancellationToken))
            .ToDictionary(r => r.UserKey, r => r.Status);
        var marks = book.Attendances
            .Where(a => a.AgendaItemKey == item.AgendaItemKey && a.OccurrenceStartUtc == occurrence.StartUtc)
            .ToDictionary(a => a.MembershipKey);
        var given = book.Entries
            .Where(e => e.AgendaItemKey == item.AgendaItemKey && e.OccurrenceStartUtc == occurrence.StartUtc)
            .ToLookup(e => e.MembershipKey ?? e.GroupKey!.Value);

        var aimedAtKurin = item.Assignments.Any(a => a.TargetType == AgendaTargetType.Kurin);
        var aimedGroups = item.Assignments.Where(a => a.TargetType == AgendaTargetType.Group).Select(a => a.TargetKey).ToHashSet();
        var aimedMembers = item.Assignments.Where(a => a.TargetType == AgendaTargetType.Member).Select(a => a.TargetKey).ToHashSet();

        var people = book.CurrentYouths()
            .Select(y => new SheetPersonDto
            {
                MembershipKey = y.Membership.MembershipKey,
                MemberKey = y.Membership.MemberKey,
                FullName = y.Person.FullName,
                GroupKey = y.Membership.GroupKey,
                GroupName = book.GroupName(y.Membership.GroupKey),
                Rsvp = y.Person.UserKey is { } user && responses.TryGetValue(user, out var status) ? status : null,
                IsAssigned = aimedAtKurin
                    || (y.Membership.GroupKey is { } g && aimedGroups.Contains(g))
                    || aimedMembers.Contains(y.Membership.MemberKey),
                Attendance = marks.TryGetValue(y.Membership.MembershipKey, out var mark)
                    ? new AttendanceMarkDto { MarkedByName = book.NameOfAccount(mark.MarkedByUserKey), MarkedAtUtc = DateTime.SpecifyKind(mark.MarkedAtUtc, DateTimeKind.Utc) }
                    : null,
                CanScore = CanScore(y.Membership.GroupKey),
                Entries = given[y.Membership.MembershipKey].OrderBy(e => e.CreatedDate).Select(book.ToDto).ToList()
            })
            .OrderBy(p => p.GroupName)
            .ThenBy(p => p.FullName)
            .ToList();

        var groups = book.GroupNames
            .OrderBy(g => g.Value)
            .Select(g => new SheetGroupDto
            {
                GroupKey = g.Key,
                GroupName = g.Value,
                CanScore = canScoreIn[g.Key],
                Entries = given[g.Key].OrderBy(e => e.CreatedDate).Select(book.ToDto).ToList()
            })
            .ToList();

        var category = await CategoryOfAsync(item.AgendaCategoryKey, cancellationToken);
        var notHeld = await _books.NotHeldAsync(item.AgendaItemKey, occurrence.StartUtc, cancellationToken);

        return new ServiceResult<AttendanceSheetResponse>(ResultType.Success, new AttendanceSheetResponse
        {
            KurinKey = request.KurinKey,
            AgendaItemKey = item.AgendaItemKey,
            OccurrenceStartUtc = occurrence.StartUtc,
            OccurrenceEndUtc = occurrence.EndUtc,
            IsAllDay = item.IsAllDay,
            IsRecurring = item.RecurrenceFrequency != RecurrenceFrequency.None,
            Title = item.Title,
            CategoryKey = category?.AgendaCategoryKey,
            CategoryName = category?.Name,
            CategoryColorHex = category?.ColorHex,
            CategoryIcon = category?.Icon,
            AttendancePoints = book.AttendancePoints(item.AgendaItemKey, item.AgendaCategoryKey),
            HasOwnRate = book.AttendanceRates.Any(r => r.AgendaItemKey == item.AgendaItemKey),
            People = people,
            Groups = groups,
            Items = book.Items.Where(i => !i.IsArchived).Select(ScoreBook.ToDto).ToList(),
            CanManage = await _access.MayManageAsync(request.KurinKey, cancellationToken),
            NotHeld = notHeld is null
                ? null
                : new OccurrenceNotHeldDto { MarkedByName = book.NameOfAccount(notHeld.MarkedByUserKey), MarkedAtUtc = DateTime.SpecifyKind(notHeld.MarkedAtUtc, DateTimeKind.Utc) }
        });
    }

    /// <summary>Who may score where, asked once per гурток rather than once per person.</summary>
    private async Task<Dictionary<Guid, bool>> CanScoreByGroupAsync(Guid kurinKey, IEnumerable<Guid> groupKeys, CancellationToken cancellationToken)
    {
        var canScoreIn = new Dictionary<Guid, bool>();
        foreach (var groupKey in groupKeys)
        {
            canScoreIn[groupKey] = await _access.MayScoreAsync(kurinKey, groupKey, ResourceAction.Create, cancellationToken);
        }

        return canScoreIn;
    }

    private async Task<AgendaCategory?> CategoryOfAsync(Guid? categoryKey, CancellationToken cancellationToken) =>
        categoryKey is { } key
            ? await _unitOfWork.AgendaCategories.GetByKeyAsync(key, cancellationToken)
            : null;
}
