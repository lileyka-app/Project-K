using MediatR;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.MemberModule;
using ProjectK.Common.Rules.Shared;

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
    private readonly IOfficeDirectory _offices;
    private readonly IMembershipDirectory _membershipRecords;
    private readonly TimeProvider _time;

    public GetMemberByKeyQueryHandler(
        IMemberUnitOfWork unitOfWork,
        IBlobReadLinks links,
        ICurrentUserContext currentUserContext,
        IMemberFieldAccess fieldAccess,
        IResourceAccessService resourceAccess,
        IUnitOfWork kurinData,
        IAccountProvisioningService accounts,
        IKurinTagVisibility tagVisibility,
        IOfficeDirectory offices,
        IMembershipDirectory membershipRecords,
        TimeProvider time)
    {
        _offices = offices;
        _membershipRecords = membershipRecords;
        _time = time;
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

        var offices = await _offices.GetHeldByMemberAsync(member.MemberKey, cancellationToken);
        var memberResponse = member.ToDto(_links, offices);
        memberResponse.KurinKey = here?.KurinKey ?? Guid.Empty;
        memberResponse.GroupKey = here?.GroupKey ?? Guid.Empty;

        // The улад as seen in this kurin: a confirmed senior ступінь outweighs it, else its гілка decides.
        var branchHere = here is null
            ? null
            : (await _membershipRecords.GetForMemberAsync(member.MemberKey, cancellationToken))
                .FirstOrDefault(m => m.IsCurrent && m.KurinKey == here.KurinKey)?.Branch;
        memberResponse.Ulad = UladRules.Of(PlastLevelHistoryRules.CountingLevels(member.PlastLevelHistory, member.LatestPlastLevel), branchHere);
        memberResponse.IsAdult = AdulthoodRules.IsAdult(member.DateOfBirth, DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime));

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
