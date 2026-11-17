using MediatR;
using Microsoft.Extensions.Logging;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;

/// <summary>
/// Sends a member whose account nobody has claimed yet a fresh invitation, to the address on their
/// record. Every invitation sent before stops working, so a letter that went astray cannot be used
/// after its replacement. Answers with the account's key; a letter that could not be sent answers
/// <c>InvitationNotSent</c> and changes no invitation.
/// </summary>
public sealed record ResendMemberInvitationCommand(Guid MemberKey) : IRequest<ServiceResult<Guid>>;

public sealed class ResendMemberInvitationCommandHandler
    : IRequestHandler<ResendMemberInvitationCommand, ServiceResult<Guid>>
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly IAccountProvisioningService _accounts;
    private readonly IEmailService _emailService;
    private readonly ILogger<ResendMemberInvitationCommandHandler> _logger;

    public ResendMemberInvitationCommandHandler(
        IMemberUnitOfWork unitOfWork,
        IAccountProvisioningService accounts,
        IEmailService emailService,
        ILogger<ResendMemberInvitationCommandHandler> logger)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _accounts = accounts;
        _emailService = emailService;
    }

    public async Task<ServiceResult<Guid>> Handle(
        ResendMemberInvitationCommand request,
        CancellationToken cancellationToken)
    {
        var member = await _unitOfWork.Members.GetByKeyAsync(request.MemberKey, cancellationToken);
        if (member is null)
        {
            return ServiceResult<Guid>.Failure(ResultType.NotFound, MemberErrors.MemberNotFound, "No such member.");
        }

        if (!member.UserKey.HasValue)
        {
            return ServiceResult<Guid>.Failure(ResultType.Conflict, MemberErrors.NoAccount, "This member has no account to invite to.");
        }

        var reissued = await _accounts.ReissueInvitationAsync(member.UserKey.Value, member.Email, cancellationToken);
        if (reissued.Type != ResultType.Success || reissued.Data is null)
        {
            return ServiceResult<Guid>.Failure(
                reissued.Type,
                reissued.ErrorCode ?? MemberErrors.InvitationNotIssued,
                reissued.ErrorMessage ?? "Could not issue a new invitation.");
        }

        // The letter goes before the save: a send that throws leaves the invitations as they were,
        // and the one the person already holds keeps working.
        try
        {
            await _emailService.SendInvitationEmailAsync(member.Email, reissued.Data.InvitationToken, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "A replacement invitation for a member could not be sent; no invitation was changed.");
            return ServiceResult<Guid>.Failure(
                ResultType.InternalServerError,
                MemberErrors.InvitationNotSent,
                "The invitation letter could not be sent.");
        }

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (changes <= 0)
        {
            return ServiceResult<Guid>.Failure(ResultType.InternalServerError, MemberErrors.MemberNotSaved, "The new invitation could not be saved.");
        }

        return new ServiceResult<Guid>(ResultType.Success, reissued.Data.UserKey);
    }
}
