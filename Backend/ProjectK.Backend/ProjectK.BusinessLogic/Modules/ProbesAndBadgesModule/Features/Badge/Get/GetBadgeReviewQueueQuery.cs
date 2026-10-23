using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Member = ProjectK.Common.Entities.KurinModule.Member;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Badge.Get;

public sealed record GetBadgeReviewQueueQuery(Guid KurinKey) : IRequest<ServiceResult<IEnumerable<BadgeProgressResponse>>>;

public sealed class GetBadgeReviewQueueQueryHandler : IRequestHandler<GetBadgeReviewQueueQuery, ServiceResult<IEnumerable<BadgeProgressResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IResourceScopeReader _scopeReader;
    private readonly IBlobReadLinks _links;
    private readonly ICurrentPermissions _permissions;

    public GetBadgeReviewQueueQueryHandler(
        IUnitOfWork unitOfWork,
        IMemberDirectory members,
        ICurrentUserContext currentUserContext,
        IResourceScopeReader scopeReader,
        IBlobReadLinks links,
        ICurrentPermissions permissions)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _currentUserContext = currentUserContext;
        _scopeReader = scopeReader;
        _links = links;
        _permissions = permissions;
    }

    public async Task<ServiceResult<IEnumerable<BadgeProgressResponse>>> Handle(GetBadgeReviewQueueQuery request, CancellationToken cancellationToken)
    {
        // Whose вмілості are in the queue is how far the kurin lets this person review them.
        var reach = await _permissions.ReachAsync(KurinCapability.ReviewBadge, cancellationToken);
        if (reach == CapabilityReach.None)
        {
            return ServiceResult<IEnumerable<BadgeProgressResponse>>.Failure(
                ResultType.Forbidden,
                "BadgeReviewNotAllowed",
                "The kurin's policy does not let you review badges.");
        }

        IEnumerable<Guid>? allowedGroupKeys = null;
        if (reach == CapabilityReach.OwnGroup)
        {
            if (_currentUserContext.UserId == null)
            {
                return new ServiceResult<IEnumerable<BadgeProgressResponse>>(ResultType.Unauthorized, null);
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

        var allProgresses = progresses
            .Where(p => p.Status == BadgeProgressStatus.Submitted)
            .Select(p => BadgeProgressResponse.FromEntity(p, membersDict[p.MemberKey], _links.For(membersDict[p.MemberKey].ProfilePhotoBlobName)))
            .OrderByDescending(p => p.SubmittedAtUtc)
            .ToList();

        return new ServiceResult<IEnumerable<BadgeProgressResponse>>(ResultType.Success, allProgresses.AsEnumerable());
    }
}
