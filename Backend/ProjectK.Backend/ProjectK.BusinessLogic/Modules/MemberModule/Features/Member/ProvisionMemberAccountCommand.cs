using MediatR;
using Microsoft.Extensions.Logging;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.MemberModule;
using ProjectK.Common.Records.IdentityModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;

/// <summary>
/// Gives an existing member the account they sign in with: a pending user, a waitlist entry
/// recorded as already approved, and the invitation email. Answers with the account's key and
/// whether the letter went out — a failed send does not undo the account.
/// </summary>
public sealed record ProvisionMemberAccountCommand(Guid MemberKey) : IRequest<ServiceResult<MemberInvitation>>;

public sealed class ProvisionMemberAccountCommandHandler
    : IRequestHandler<ProvisionMemberAccountCommand, ServiceResult<MemberInvitation>>
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly IAccountProvisioningService _accountProvisioning;
    private readonly IDomainEventPublisher _events;
    private readonly IEmailService _emailService;
    private readonly ILogger<ProvisionMemberAccountCommandHandler> _logger;

    public ProvisionMemberAccountCommandHandler(
        IMemberUnitOfWork unitOfWork,
        IAccountProvisioningService accountProvisioning,
        IDomainEventPublisher events,
        IEmailService emailService,
        ILogger<ProvisionMemberAccountCommandHandler> logger)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _accountProvisioning = accountProvisioning;
        _events = events;
        _emailService = emailService;
    }

    public async Task<ServiceResult<MemberInvitation>> Handle(
        ProvisionMemberAccountCommand request,
        CancellationToken cancellationToken)
    {
        var member = await _unitOfWork.Members.GetByKeyAsync(request.MemberKey, cancellationToken);
        if (member is null)
        {
            return ServiceResult<MemberInvitation>.Failure(ResultType.NotFound, MemberErrors.MemberNotFound, "No such member.");
        }

        if (member.UserKey.HasValue)
        {
            return ServiceResult<MemberInvitation>.Failure(ResultType.Conflict, MemberErrors.AccountAlreadyLinked, "This member already has an account.");
        }

        var availability = await _accountProvisioning.CheckAvailabilityAsync(member.Email, cancellationToken);
        if (availability != AccountAvailability.Available)
        {
            return ServiceResult<MemberInvitation>.Failure(ResultType.Conflict, MemberErrors.AccountEmailTaken, "This email is already used by another account or registration.");
        }

        var provisioned = await _accountProvisioning.ProvisionAsync(
            new AccountProvisioningRequest(
                member.Email,
                member.FirstName,
                member.LastName,
                WaitlistEntryKey: null,
                member.PhoneNumber,
                member.DateOfBirth),
            cancellationToken);

        if (provisioned.Type != ResultType.Success || provisioned.Data is null)
        {
            return ServiceResult<MemberInvitation>.Failure(
                provisioned.Type,
                provisioned.ErrorCode ?? MemberErrors.UserNotCreated,
                provisioned.ErrorMessage ?? "Failed to create user account for member.");
        }

        member.UserKey = provisioned.Data.UserKey;
        _unitOfWork.Members.Update(member, cancellationToken);
        await _events.PublishAsync(
            new MemberAccountLinked(member.MemberKey, provisioned.Data.UserKey),
            cancellationToken);

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (changes <= 0)
        {
            return ServiceResult<MemberInvitation>.Failure(ResultType.InternalServerError, MemberErrors.MemberNotSaved, "The account link could not be saved.");
        }

        // Everything above is already committed, so a send that throws must not turn into a
        // failure: the провід was told "not created" while the member and the account stood, and
        // the retry then hit a taken address. The account is reported as opened, the letter as not
        // sent, and it can be sent again from the member.
        try
        {
            await _emailService.SendInvitationEmailAsync(
                member.Email,
                provisioned.Data.InvitationToken,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "An account was opened for a member, but the invitation letter could not be sent.");
            return new ServiceResult<MemberInvitation>(
                ResultType.Success,
                new MemberInvitation(provisioned.Data.UserKey, Sent: false));
        }

        return new ServiceResult<MemberInvitation>(
            ResultType.Success,
            new MemberInvitation(provisioned.Data.UserKey, Sent: true));
    }
}
