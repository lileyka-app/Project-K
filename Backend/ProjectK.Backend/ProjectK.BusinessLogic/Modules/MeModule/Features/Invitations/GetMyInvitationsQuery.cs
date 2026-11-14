using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Models.Dtos.MeModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Invitations;

/// <summary>
/// The kurins waiting on the signed-in person's own answer: requests across the youth and senior
/// lines that no kurin answers for them (<see cref="MembershipAskRouting"/>).
/// </summary>
public sealed record GetMyInvitationsQuery : IRequest<ServiceResult<IReadOnlyList<MembershipInvitationDto>>>;

public sealed class GetMyInvitationsQueryHandler
    : IRequestHandler<GetMyInvitationsQuery, ServiceResult<IReadOnlyList<MembershipInvitationDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public GetMyInvitationsQueryHandler(IUnitOfWork unitOfWork, IMemberDirectory members, ICurrentUserContext currentUser, TimeProvider time)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<IReadOnlyList<MembershipInvitationDto>>> Handle(GetMyInvitationsQuery request, CancellationToken cancellationToken)
    {
        var me = _currentUser.UserId is { } userKey ? await _members.FindByAccountAsync(userKey, cancellationToken) : null;
        if (me is null)
        {
            return new ServiceResult<IReadOnlyList<MembershipInvitationDto>>(ResultType.Success, []);
        }

        var open = await _unitOfWork.MembershipRequests.GetOpenForPersonAsync(me.MemberKey, _time.GetUtcNow().UtcDateTime, cancellationToken);
        var result = new List<MembershipInvitationDto>(open.Count);
        foreach (var invitation in open)
        {
            var kurin = await _unitOfWork.Kurins.GetByKeyAsync(invitation.ToKurinKey, cancellationToken);
            result.Add(new MembershipInvitationDto(
                invitation.MembershipRequestKey,
                kurin.ToRequestKurinDto(invitation.ToKurinKey),
                invitation.MembershipKind,
                invitation.CreatedAtUtc,
                invitation.ExpiresAtUtc));
        }

        return new ServiceResult<IReadOnlyList<MembershipInvitationDto>>(ResultType.Success, result);
    }
}
