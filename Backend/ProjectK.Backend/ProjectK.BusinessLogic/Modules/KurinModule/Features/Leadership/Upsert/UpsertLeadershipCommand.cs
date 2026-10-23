using AutoMapper;
using MediatR;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
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

    public UpsertLeadershipCommandHandler(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ICurrentUserContext currentUserContext,
        IMemberDirectory members,
        IBackendCache cache,
        IAccessChangeSignal accessChanges)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _currentUserContext = currentUserContext;
        _members = members;
        _cache = cache;
        _accessChanges = accessChanges;
    }

    public async Task<ServiceResult<LeadershipResponse>> Handle(UpsertLeadershipCommand request, CancellationToken cancellationToken)
    {
        LeadershipEntity? existing = null;
        bool isCreated = false;
        IReadOnlyCollection<Guid> touchedHolders;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.LeadershipKey != null && request.LeadershipKey != Guid.Empty)
        {
            existing = await _unitOfWork.Leaderships.GetByKeyAsync(request.LeadershipKey.Value, cancellationToken);
        }

        if (existing != null)
        {
            // Update existing Leadership
            existing.Type = Enum.Parse<Common.Models.Enums.LeadershipType>(request.Type!, ignoreCase: true);
            PlaceOnEntity(existing, request.EntityKey);
            if (!request.SeatedBySystem && !MayAssignTouchedOffices(existing, request.LeadershipHistoryMembers, existing.Type))
            {
                return new ServiceResult<LeadershipResponse>(ResultType.Forbidden);
            }

            touchedHolders = ApplyLeadershipHistoryChanges(existing, request.LeadershipHistoryMembers, today);
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

            touchedHolders = ApplyLeadershipHistoryChanges(existing, request.LeadershipHistoryMembers, today);
            _unitOfWork.Leaderships.Add(existing, cancellationToken);
            isCreated = true;
        }

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
            await MarkHoldersAsync(touchedHolders, cancellationToken);
        }

        var response = _mapper.Map<LeadershipResponse>(existing);

        return isCreated
            ? new ServiceResult<LeadershipResponse>(ResultType.Created, response, CreatedAtActionName: "GetLeadershipByKey", CreatedAtRouteValues: new { leadershipKey = response.LeadershipKey })
            : new ServiceResult<LeadershipResponse>(ResultType.Success, response);
    }

    private async Task MarkHoldersAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken)
    {
        var holders = await _members.GetByKeysAsync(memberKeys, cancellationToken);
        foreach (var userKey in holders.Select(holder => holder.UserKey).OfType<Guid>().Distinct())
        {
            _accessChanges.MarkUser(userKey);
        }
    }

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
    /// Ends the holders the request no longer names and seats the new ones. Answers the members
    /// whose offices changed either way.
    /// </summary>
    private static HashSet<Guid> ApplyLeadershipHistoryChanges(
        LeadershipEntity leadership,
        IEnumerable<LeadershipHistoryMemberDto> requestedHistories,
        DateOnly today)
    {
        var touched = new HashSet<Guid>();
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
                touched.Add(activeHistory.MemberKey);
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
            touched.Add(assignment.MemberKey);
        }

        return touched;
    }
}
