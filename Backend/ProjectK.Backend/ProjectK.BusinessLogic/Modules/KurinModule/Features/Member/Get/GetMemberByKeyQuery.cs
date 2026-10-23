using ProjectK.Common.Models.Members;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Get;

public class GetMemberByKeyQuery : IRequest<ServiceResult<MemberResponse>>
{
    public Guid MemberKey { get; set; }
    public GetMemberByKeyQuery(Guid memberKey)
    {
        MemberKey = memberKey;
    }
}

public class GetMemberByKeyQueryHandler : IRequestHandler<GetMemberByKeyQuery, ServiceResult<MemberResponse>>
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IMemberFieldAccess _fieldAccess;
    private readonly IResourceAccessService _resourceAccess;
    private readonly IMembershipRepository _memberships;
    private readonly IMentorAssignmentRepository _mentorAssignments;
    private readonly IAccountProvisioningService _accounts;

    public GetMemberByKeyQueryHandler(
        IMemberUnitOfWork unitOfWork,
        IMapper mapper,
        ICurrentUserContext currentUserContext,
        IMemberFieldAccess fieldAccess,
        IResourceAccessService resourceAccess,
        IUnitOfWork kurinData,
        IAccountProvisioningService accounts)
    {
        _accounts = accounts;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _currentUserContext = currentUserContext;
        _fieldAccess = fieldAccess;
        _resourceAccess = resourceAccess;
        _memberships = kurinData.Memberships;
        _mentorAssignments = kurinData.MentorAssignments;
    }

    public async Task<ServiceResult<MemberResponse>> Handle(GetMemberByKeyQuery request, CancellationToken cancellationToken)
    {
        var member = await _unitOfWork.Members.GetByKeyAsync(request.MemberKey, cancellationToken);
        if (member is null)
        {
            return new ServiceResult<MemberResponse>(ResultType.NotFound);
        }

        var placement = await _memberships.GetActiveForMemberAsync(request.MemberKey, cancellationToken);
        var here = placement.FirstOrDefault(m => m.KurinKey == _currentUserContext.KurinKey)
            ?? placement.FirstOrDefault();

        var memberResponse = _mapper.Map<MemberResponse>(member);
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

        // Whether the invitation is still waiting is for whoever may act on it: the people who may edit them.
        if (member.UserKey.HasValue
            && (await _resourceAccess.CheckAccessAsync(ResourceType.Member, ResourceAction.Update, member.MemberKey, cancellationToken)).IsAllowed)
        {
            memberResponse.AccountStatus = (await _accounts.FindAsync(member.UserKey.Value, cancellationToken))?.Status;
        }

        return new ServiceResult<MemberResponse>(ResultType.Success, memberResponse);
    }
}
