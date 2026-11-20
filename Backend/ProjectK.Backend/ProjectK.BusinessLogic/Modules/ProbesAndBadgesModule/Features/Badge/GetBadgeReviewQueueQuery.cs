using MediatR;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Badge;

public sealed record GetBadgeReviewQueueQuery(Guid KurinKey) : IRequest<ServiceResult<IEnumerable<BadgeProgressDto>>>;

public sealed class GetBadgeReviewQueueQueryHandler : IRequestHandler<GetBadgeReviewQueueQuery, ServiceResult<IEnumerable<BadgeProgressDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMembershipDirectory _memberships;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IResourceScopeReader _scopeReader;
    private readonly IBlobReadLinks _links;
    private readonly ICurrentPermissions _permissions;
    private readonly IKurinJournalRepository _journal;

    public GetBadgeReviewQueueQueryHandler(
        IUnitOfWork unitOfWork,
        IMembershipDirectory memberships,
        ICurrentUserContext currentUserContext,
        IBlobReadLinks links,
        ICurrentPermissions permissions,
        IAuditUnitOfWork audit,
        IResourceScopeReader scopeReader)
    {
        _unitOfWork = unitOfWork;
        _memberships = memberships;
        _currentUserContext = currentUserContext;
        _scopeReader = scopeReader;
        _links = links;
        _permissions = permissions;
        _journal = audit.KurinJournal;
    }

    public async Task<ServiceResult<IEnumerable<BadgeProgressDto>>> Handle(GetBadgeReviewQueueQuery request, CancellationToken cancellationToken)
    {
        // Whose вмілості are in the queue is how far the kurin lets this person review them.
        var reach = await _permissions.ReachAsync(KurinCapability.ReviewBadge, cancellationToken);
        if (reach == CapabilityReach.None)
        {
            return ServiceResult<IEnumerable<BadgeProgressDto>>.Failure(
                ResultType.Forbidden,
                ProbesAndBadgesErrors.BadgeReviewNotAllowed,
                "The kurin's policy does not let you review badges.");
        }

        IEnumerable<Guid>? allowedGroupKeys = null;
        if (reach == CapabilityReach.OwnGroup)
        {
            if (_currentUserContext.UserId == null)
            {
                return ServiceResult<IEnumerable<BadgeProgressDto>>.Failure(ResultType.Unauthorized, ProbesAndBadgesErrors.Unauthorized, "No one is signed in.");
            }
            allowedGroupKeys = await _scopeReader.GetLedGroupKeysAsync(_currentUserContext.UserId.Value, request.KurinKey, cancellationToken);
        }

        var membersInKurin = await _memberships.GetPeopleInKurinAsync(request.KurinKey, cancellationToken);
        var membersDict = membersInKurin.ToDictionary(m => m.MemberKey);

        var filteredMembers = allowedGroupKeys != null
            ? membersDict.Values.Where(m => m.GroupKey.HasValue && allowedGroupKeys.Contains(m.GroupKey.Value))
            : membersDict.Values;

        var memberKeys = filteredMembers.Select(m => m.MemberKey).ToList();

        var progresses = await _unitOfWork.BadgeProgresses.GetByMemberKeysAsync(memberKeys, cancellationToken);

        var submitted = progresses.Where(p => p.Status == BadgeProgressStatus.Submitted).ToList();
        var journal = (await _journal.GetForSubjectsAsync(submitted.Select(p => p.BadgeProgressKey).ToList(), cancellationToken))
            .ToLookup(entry => entry.SubjectKey);

        var allProgresses = submitted
            .Select(p => BadgeMapping.ToDto(p, journal[p.BadgeProgressKey], membersDict[p.MemberKey].Person, _links.For(membersDict[p.MemberKey].Person.ProfilePhotoBlobName)))
            .OrderByDescending(p => p.SubmittedAtUtc)
            .ToList();

        return new ServiceResult<IEnumerable<BadgeProgressDto>>(ResultType.Success, allProgresses.AsEnumerable());
    }
}
