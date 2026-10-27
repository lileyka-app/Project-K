using AutoMapper;
using MediatR;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Reports;
using LeadershipEntity = ProjectK.Common.Entities.KurinModule.Leadership;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership.Upsert;

public class UpsertLeadershipCommand : IRequest<ServiceResult<LeadershipResponse>>
{
    public Guid? LeadershipKey { get; set; }
    public string? Type { get; set; }
    public Guid? EntityKey { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public IEnumerable<LeadershipHistoryMemberDto> LeadershipHistoryMembers { get; set; }

    /// <summary>
    /// Set only by server-side flows that seat an office with no signed-in assigner — account
    /// activation makes the new kurin leader the Зв'язковий while the caller is still anonymous.
    /// It is absent from <see cref="UpsertLeadershipRequest"/>, so an HTTP caller cannot set it.
    /// </summary>
    public bool SeatedBySystem { get; init; }

    public UpsertLeadershipCommand(UpsertLeadershipRequest request)
    {
        Type = request.Type;
        EntityKey = request.EntityKey;
        StartDate = request.StartDate;
        EndDate = request.EndDate;
        LeadershipHistoryMembers = request.LeadershipHistories;
    }
    public UpsertLeadershipCommand(UpsertLeadershipRequest request, Guid leadershipKey)
    {
        LeadershipKey = leadershipKey;
        Type = request.Type;
        EntityKey = request.EntityKey;
        StartDate = request.StartDate;
        EndDate = request.EndDate;
        LeadershipHistoryMembers = request.LeadershipHistories;
    }
}

public class UpsertLeadershipCommandHandler : IRequestHandler<UpsertLeadershipCommand, ServiceResult<LeadershipResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IMemberDirectory _members;
    private readonly IBackendCache _cache;
    private readonly IAccessChangeSignal _accessChanges;
    private readonly IKurinAudit _audit;

    public UpsertLeadershipCommandHandler(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ICurrentUserContext currentUserContext,
        IMemberDirectory members,
        IBackendCache cache,
        IAccessChangeSignal accessChanges,
        IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _currentUserContext = currentUserContext;
        _members = members;
        _cache = cache;
        _accessChanges = accessChanges;
        _audit = audit;
    }

    public async Task<ServiceResult<LeadershipResponse>> Handle(UpsertLeadershipCommand request, CancellationToken cancellationToken)
    {
        LeadershipEntity? existing = null;
        bool isCreated = false;
        var moved = false;
        IReadOnlyList<OfficeChange> officeChanges;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.LeadershipKey != null && request.LeadershipKey != Guid.Empty)
        {
            existing = await _unitOfWork.Leaderships.GetByKeyAsync(request.LeadershipKey.Value, cancellationToken);
        }

        if (existing != null)
        {
            // Update existing Leadership
            var placedBefore = (existing.Type, existing.KurinKey, existing.GroupKey);
            existing.Type = Enum.Parse<Common.Models.Enums.LeadershipType>(request.Type!, ignoreCase: true);
            PlaceOnEntity(existing, request.EntityKey);
            moved = placedBefore != (existing.Type, existing.KurinKey, existing.GroupKey);
            if (!request.SeatedBySystem && !MayAssignTouchedOffices(existing, request.LeadershipHistoryMembers, existing.Type))
            {
                return new ServiceResult<LeadershipResponse>(ResultType.Forbidden);
            }

            officeChanges = ApplyLeadershipHistoryChanges(existing, request.LeadershipHistoryMembers, today);
            _unitOfWork.Leaderships.Update(existing, cancellationToken);
        }
        else
        {
            // Create new Leadership
            existing = _mapper.Map<LeadershipEntity>(request);
            if (request.LeadershipKey is Guid leadershipKey && leadershipKey != Guid.Empty)
            {
                existing.LeadershipKey = leadershipKey;
            }

            existing.StartDate = today;
            existing.EndDate = null;
            existing.Type = Enum.Parse<Common.Models.Enums.LeadershipType>(request.Type!, ignoreCase: true);
            PlaceOnEntity(existing, request.EntityKey);
            existing.LeadershipHistories.Clear();
            if (!request.SeatedBySystem && !MayAssignTouchedOffices(existing, request.LeadershipHistoryMembers, existing.Type))
            {
                return new ServiceResult<LeadershipResponse>(ResultType.Forbidden);
            }

            officeChanges = ApplyLeadershipHistoryChanges(existing, request.LeadershipHistoryMembers, today);
            _unitOfWork.Leaderships.Add(existing, cancellationToken);
            isCreated = true;
        }

        var touchedHolders = officeChanges.SelectMany(change => change.Ended.Concat(change.Seated)).ToHashSet();
        if (moved)
        {
            // A провід moved to another гурток or курінь changes what every sitting holder may do,
            // though none of them was seated or unseated.
            touchedHolders.UnionWith(existing.LeadershipHistories.Where(history => history.EndDate == null).Select(history => history.MemberKey));
        }
        IReadOnlyCollection<MemberSummary> holders = touchedHolders.Count == 0
            ? []
            : await _members.GetByKeysAsync(touchedHolders, cancellationToken);
        await RecordAsync(existing, officeChanges, holders, cancellationToken);

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (changes <= 0)
        {
            return new ServiceResult<LeadershipResponse>(ResultType.InternalServerError);
        }

        // What an office grants is worked out from this registry when a token is minted. Whoever
        // was seated or unseated is told their token is stale, so the change reaches them on their
        // next request; the cached led-group sets are read from offices too and must not lag.
        if (touchedHolders.Count > 0)
        {
            _cache.Invalidate(BackendCachePolicies.MentorScopeReads);
            MarkHolders(holders);
        }

        var response = _mapper.Map<LeadershipResponse>(existing);

        return isCreated
            ? new ServiceResult<LeadershipResponse>(ResultType.Created, response, CreatedAtActionName: "GetLeadershipByKey", CreatedAtRouteValues: new { leadershipKey = response.LeadershipKey })
            : new ServiceResult<LeadershipResponse>(ResultType.Success, response);
    }

    private void MarkHolders(IReadOnlyCollection<MemberSummary> holders)
    {
        foreach (var userKey in holders.Select(holder => holder.UserKey).OfType<Guid>().Distinct())
        {
            _accessChanges.MarkUser(userKey);
        }
    }

    /// <summary>
    /// One journal entry per role that changed hands: who held it and who holds it now. The entry is
    /// about the new holder, or about the one who left when nobody took their place.
    /// </summary>
    private async Task RecordAsync(
        LeadershipEntity leadership,
        IReadOnlyList<OfficeChange> officeChanges,
        IReadOnlyCollection<MemberSummary> holders,
        CancellationToken cancellationToken)
    {
        var names = holders.GroupBy(holder => holder.MemberKey).ToDictionary(found => found.Key, found => found.First().FullName);
        foreach (var change in officeChanges)
        {
            var seated = change.Seated.Count > 0;
            var subject = seated ? change.Seated : change.Ended;
            await _audit.RecordAsync(
                new AuditRecord(
                    AuditArea.Office,
                    seated ? AuditActions.Office.Assigned : AuditActions.Office.Ended,
                    leadership.LeadershipKey,
                    KurinReportTerminology.LeadershipRole(change.Role),
                    subject.Count == 1 ? subject[0] : null,
                    AuditChanges.Of(("holder", NamesOf(change.Ended, names), NamesOf(change.Seated, names))),
                    KurinKey: leadership.KurinKey),
                cancellationToken);
        }
    }

    private static string? NamesOf(List<Guid> memberKeys, Dictionary<Guid, string> names) =>
        memberKeys.Count == 0
            ? null
            : string.Join(", ", memberKeys.Select(key => names.GetValueOrDefault(key, string.Empty)).Where(name => name.Length > 0).Order(StringComparer.Ordinal));

    private static void PlaceOnEntity(LeadershipEntity leadership, Guid? entityKey)
    {
        switch (leadership.Type)
        {
            case LeadershipType.Kurin or LeadershipType.KV:
                leadership.KurinKey = entityKey;
                leadership.GroupKey = null;
                break;
            case LeadershipType.Group:
                leadership.GroupKey = entityKey;
                leadership.KurinKey = null;
                break;
        }
    }

    /// <summary>
    /// Rejects the request when it would seat or unseat an office the caller may not assign. Only
    /// the offices actually changing are checked — resubmitting an untouched провід stays allowed.
    /// </summary>
    private bool MayAssignTouchedOffices(
        LeadershipEntity leadership,
        IEnumerable<LeadershipHistoryMemberDto> requestedHistories,
        LeadershipType type)
    {
        var assignable = RolePermissionMap.AssignableOffices(_currentUserContext.Roles);
        var requested = ParseRequestedAssignments(requestedHistories);
        var active = leadership.LeadershipHistories
            .Where(history => history.EndDate == null)
            .ToList();

        var touched = new HashSet<LeadershipRole>();
        foreach (var activeHistory in active)
        {
            var isStillAssigned = requested.Any(assignment =>
                assignment.Role == activeHistory.Role && assignment.MemberKey == activeHistory.MemberKey);
            if (!isStillAssigned)
            {
                touched.Add(activeHistory.Role);
            }
        }

        foreach (var assignment in requested)
        {
            var alreadyActive = active.Any(history =>
                history.Role == assignment.Role && history.MemberKey == assignment.MemberKey);
            if (!alreadyActive)
            {
                touched.Add(assignment.Role);
            }
        }

        return touched.All(role => assignable.Contains((type, role)));
    }

    private static List<(LeadershipRole Role, Guid MemberKey)> ParseRequestedAssignments(
        IEnumerable<LeadershipHistoryMemberDto> requestedHistories) =>
        requestedHistories
            .Where(history => history.EndDate == null)
            .Where(history => history.Member?.MemberKey is Guid memberKey && memberKey != Guid.Empty)
            .Where(history => !string.IsNullOrWhiteSpace(history.Role))
            .Select(history => (
                Role: Enum.Parse<LeadershipRole>(history.Role, ignoreCase: true),
                MemberKey: history.Member.MemberKey))
            .Distinct()
            .ToList();

    /// <summary>
    /// Ends the holders the request no longer names and seats the new ones. Answers each role that
    /// changed hands, with who left it and who took it.
    /// </summary>
    private static List<OfficeChange> ApplyLeadershipHistoryChanges(
        LeadershipEntity leadership,
        IEnumerable<LeadershipHistoryMemberDto> requestedHistories,
        DateOnly today)
    {
        var touched = new Dictionary<LeadershipRole, OfficeChange>();
        OfficeChange ChangeOf(LeadershipRole role)
        {
            if (!touched.TryGetValue(role, out var change))
            {
                change = new OfficeChange(role, [], []);
                touched[role] = change;
            }

            return change;
        }

        var requestedAssignments = requestedHistories
            .Where(history => history.EndDate == null)
            .Where(history => history.Member?.MemberKey is Guid memberKey && memberKey != Guid.Empty)
            .Where(history => !string.IsNullOrWhiteSpace(history.Role))
            .Select(history => new
            {
                Role = Enum.Parse<LeadershipRole>(history.Role, ignoreCase: true),
                MemberKey = history.Member.MemberKey
            })
            .Distinct()
            .ToList();

        var activeHistories = leadership.LeadershipHistories
            .Where(history => history.EndDate == null)
            .ToList();

        foreach (var activeHistory in activeHistories)
        {
            var isStillAssigned = requestedAssignments.Any(assignment =>
                assignment.Role == activeHistory.Role &&
                assignment.MemberKey == activeHistory.MemberKey);

            if (!isStillAssigned)
            {
                activeHistory.EndDate = today;
                ChangeOf(activeHistory.Role).Ended.Add(activeHistory.MemberKey);
            }
        }

        foreach (var assignment in requestedAssignments)
        {
            var alreadyActive = activeHistories.Any(history =>
                history.Role == assignment.Role &&
                history.MemberKey == assignment.MemberKey);

            if (alreadyActive)
            {
                continue;
            }

            leadership.LeadershipHistories.Add(new Common.Entities.KurinModule.LeadershipHistory
            {
                Leadership = leadership,
                LeadershipKey = leadership.LeadershipKey,
                Role = assignment.Role,
                MemberKey = assignment.MemberKey,
                StartDate = today,
                EndDate = null
            });
            ChangeOf(assignment.Role).Seated.Add(assignment.MemberKey);
        }

        return touched.Values.OrderBy(change => change.Role).ToList();
    }

    /// <summary>A role that changed hands: the people who left it and the people who took it.</summary>
    private sealed record OfficeChange(LeadershipRole Role, List<Guid> Ended, List<Guid> Seated);
}
