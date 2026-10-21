using MediatR;
using Microsoft.Extensions.Logging;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Account;

public class ProvisionMemberAccountCommandHandler
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
            return new ServiceResult<MemberInvitation>(ResultType.NotFound);
        }

        if (member.UserKey.HasValue)
        {
            return new ServiceResult<MemberInvitation>(ResultType.Conflict);
        }

        var availability = await _accountProvisioning.CheckAvailabilityAsync(member.Email, cancellationToken);
        if (availability != AccountAvailability.Available)
        {
            return new ServiceResult<MemberInvitation>(ResultType.Conflict);
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
                provisioned.ErrorCode ?? "UserNotCreated",
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
            return new ServiceResult<MemberInvitation>(ResultType.InternalServerError);
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
