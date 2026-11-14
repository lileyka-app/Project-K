using MediatR;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Badge;

public sealed record GetBadgeReviewQueueQuery(Guid KurinKey) : IRequest<ServiceResult<IEnumerable<BadgeProgressDto>>>;

public sealed class GetBadgeReviewQueueQueryHandler : IRequestHandler<GetBadgeReviewQueueQuery, ServiceResult<IEnumerable<BadgeProgressDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IResourceScopeReader _scopeReader;
    private readonly IBlobReadLinks _links;
    private readonly ICurrentPermissions _permissions;
    private readonly IKurinJournalReader _journal;

    public GetBadgeReviewQueueQueryHandler(
        IUnitOfWork unitOfWork,
        IMemberDirectory members,
        ICurrentUserContext currentUserContext,
        IResourceScopeReader scopeReader,
        IBlobReadLinks links,
        ICurrentPermissions permissions,
        IKurinJournalReader journal)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _currentUserContext = currentUserContext;
        _scopeReader = scopeReader;
        _links = links;
        _permissions = permissions;
        _journal = journal;
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

        var membersInKurin = await _members.GetByKurinAsync(request.KurinKey, cancellationToken);
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
            .Select(p => BadgeMapping.ToDto(p, journal[p.BadgeProgressKey], membersDict[p.MemberKey], _links.For(membersDict[p.MemberKey].ProfilePhotoBlobName)))
            .OrderByDescending(p => p.SubmittedAtUtc)
            .ToList();

        return new ServiceResult<IEnumerable<BadgeProgressDto>>(ResultType.Success, allProgresses.AsEnumerable());
    }
}
