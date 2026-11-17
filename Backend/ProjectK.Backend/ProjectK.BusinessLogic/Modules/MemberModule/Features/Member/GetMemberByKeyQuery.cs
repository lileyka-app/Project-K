using MediatR;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;

public sealed class GetMemberByKeyQuery : IRequest<ServiceResult<MemberDto>>
{
    public Guid MemberKey { get; set; }
    public GetMemberByKeyQuery(Guid memberKey)
    {
        MemberKey = memberKey;
    }
}

public sealed class GetMemberByKeyQueryHandler : IRequestHandler<GetMemberByKeyQuery, ServiceResult<MemberDto>>
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly IBlobReadLinks _links;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IMemberFieldAccess _fieldAccess;
    private readonly IResourceAccessService _resourceAccess;
    private readonly IMembershipRepository _memberships;
    private readonly IMentorAssignmentRepository _mentorAssignments;
    private readonly IAccountProvisioningService _accounts;
    private readonly IKurinTagVisibility _tagVisibility;
    private readonly IKurinDirectory _kurins;

    public GetMemberByKeyQueryHandler(
        IMemberUnitOfWork unitOfWork,
        IBlobReadLinks links,
        ICurrentUserContext currentUserContext,
        IMemberFieldAccess fieldAccess,
        IResourceAccessService resourceAccess,
        IUnitOfWork kurinData,
        IAccountProvisioningService accounts,
        IKurinTagVisibility tagVisibility,
        IKurinDirectory kurins)
    {
        _kurins = kurins;
        _accounts = accounts;
        _tagVisibility = tagVisibility;
        _unitOfWork = unitOfWork;
        _links = links;
        _currentUserContext = currentUserContext;
        _fieldAccess = fieldAccess;
        _resourceAccess = resourceAccess;
        _memberships = kurinData.Memberships;
        _mentorAssignments = kurinData.MentorAssignments;
    }

    public async Task<ServiceResult<MemberDto>> Handle(GetMemberByKeyQuery request, CancellationToken cancellationToken)
    {
        var member = await _unitOfWork.Members.GetByKeyAsync(request.MemberKey, cancellationToken);
        if (member is null)
        {
            return ServiceResult<MemberDto>.Failure(ResultType.NotFound, MemberErrors.MemberNotFound, "No such member.");
        }

        var placement = await _memberships.GetActiveForMemberAsync(request.MemberKey, cancellationToken);
        var here = placement.FirstOrDefault(m => m.KurinKey == _currentUserContext.KurinKey)
            ?? placement.FirstOrDefault();

        var officeGroups = await _kurins.GetGroupsAsync(MemberMapping.OfficeGroupKeys(member), cancellationToken);
        var memberResponse = member.ToDto(_links, officeGroups);
        memberResponse.KurinKey = here?.KurinKey ?? Guid.Empty;
        memberResponse.GroupKey = here?.GroupKey ?? Guid.Empty;

        // Assignments are kept per account, so a person without one runs no гурток.
        if (here is not null && member.UserKey.HasValue)
        {
            memberResponse.MentoredGroups = [.. await _mentorAssignments
                .GetActiveGroupsAsync(member.UserKey.Value, here.KurinKey, cancellationToken)];
            memberResponse.MentoredGroupNames = [.. memberResponse.MentoredGroups.Select(g => g.Name)];
        }

        memberResponse.MaskFor(await _fieldAccess.ForViewerAsync(cancellationToken), here?.GroupKey);
        if (here is not null)
        {
            await _tagVisibility.FillAsync(here.KurinKey, [memberResponse], cancellationToken);
        }

        // Whether the invitation is still waiting is for whoever may act on it: the people who may edit them.
        if (member.UserKey.HasValue
            && (await _resourceAccess.CheckAccessAsync(ResourceType.Member, ResourceAction.Update, member.MemberKey, cancellationToken)).IsAllowed)
        {
            memberResponse.AccountStatus = (await _accounts.FindAsync(member.UserKey.Value, cancellationToken))?.Status;
        }

        return new ServiceResult<MemberDto>(ResultType.Success, memberResponse);
    }
}
