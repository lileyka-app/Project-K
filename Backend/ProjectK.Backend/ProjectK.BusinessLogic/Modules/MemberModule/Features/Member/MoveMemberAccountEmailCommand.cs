using MediatR;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;

/// <summary>
/// Moves a member's account to the address now on their record, so the two never sign in under
/// different addresses. An account nobody has claimed yet is sent a fresh invitation there; one in
/// use is moved and its sessions ended. Answers with the account's key.
/// </summary>
public sealed record MoveMemberAccountEmailCommand(Guid MemberKey) : IRequest<ServiceResult<Guid>>;

public sealed class MoveMemberAccountEmailCommandHandler
    : IRequestHandler<MoveMemberAccountEmailCommand, ServiceResult<Guid>>
{
    private readonly IMediator _mediator;
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly IAccountProvisioningService _accounts;

    public MoveMemberAccountEmailCommandHandler(
        IMediator mediator,
        IMemberUnitOfWork unitOfWork,
        IAccountProvisioningService accounts)
    {
        _mediator = mediator;
        _unitOfWork = unitOfWork;
        _accounts = accounts;
    }

    public async Task<ServiceResult<Guid>> Handle(
        MoveMemberAccountEmailCommand request,
        CancellationToken cancellationToken)
    {
        var member = await _unitOfWork.Members.GetByKeyAsync(request.MemberKey, cancellationToken);
        if (member?.UserKey is not { } userKey)
        {
            return ServiceResult<Guid>.Failure(ResultType.NotFound, MemberErrors.MemberNotFound, "No such member with an account.");
        }

        var account = await _accounts.FindAsync(userKey, cancellationToken);
        if (account is null)
        {
            return ServiceResult<Guid>.Failure(ResultType.NotFound, MemberErrors.AccountNotFound, "The member's account does not exist.");
        }

        return account.Status == OnboardingStatus.PendingActivation
            ? await _mediator.Send(new ResendMemberInvitationCommand(member.MemberKey), cancellationToken)
            : await _accounts.ChangeEmailAsync(userKey, member.Email, cancellationToken);
    }
}
