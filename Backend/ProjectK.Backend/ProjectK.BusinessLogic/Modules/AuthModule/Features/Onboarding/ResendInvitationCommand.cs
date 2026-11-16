using MediatR;
using ProjectK.BusinessLogic.Behaviors;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuthModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding;

public sealed record ResendInvitationCommand(Guid WaitlistEntryKey) : IRequest<ServiceResult<Guid>>, ITransactionalRequest;

public sealed class ResendInvitationCommandHandler : IRequestHandler<ResendInvitationCommand, ServiceResult<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly TimeProvider _timeProvider;

    public ResendInvitationCommandHandler(IUnitOfWork unitOfWork, IEmailService emailService, TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _timeProvider = timeProvider;
    }

    public async Task<ServiceResult<Guid>> Handle(ResendInvitationCommand request, CancellationToken cancellationToken)
    {
        var entry = await _unitOfWork.WaitlistEntries.GetByKeyAsync(request.WaitlistEntryKey, cancellationToken);
        if (entry == null)
        {
            return ServiceResult<Guid>.Failure(ResultType.NotFound, AuthErrors.WaitlistEntryNotFound, "Waitlist entry not found.");
        }

        // Holding no live invitation is the normal state of someone who needs one resent: theirs
        // expired, or retention swept it up. Refusing here left the panel unable to help exactly
        // the people it exists for, so a resend now issues one whether or not it replaces.
        var invitation = await _unitOfWork.Invitations.GetActiveByWaitlistEntryKeyAsync(request.WaitlistEntryKey, cancellationToken);
        var targetUser = await _unitOfWork.Users.GetByEmailAsync(entry.Email, cancellationToken);

        var newInvitation = new Invitation
        {
            InvitationKey = Guid.NewGuid(),
            Token = Guid.NewGuid().ToString("N"),
            WaitlistEntryKey = entry.WaitlistEntryKey,
            TargetUserKey = invitation?.TargetUserKey ?? targetUser?.Id,
            ExpiresAtUtc = _timeProvider.GetUtcNow().UtcDateTime.AddDays(OnboardingRules.InvitationLifetimeDays)
        };

        await _emailService.SendInvitationEmailAsync(entry.Email, newInvitation.Token, cancellationToken);

        if (invitation is not null)
        {
            invitation.IsRevoked = true;
            _unitOfWork.Invitations.Update(invitation, cancellationToken);
        }

        _unitOfWork.Invitations.Create(newInvitation, cancellationToken);

        entry.InvitationSentAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        _unitOfWork.WaitlistEntries.Update(entry, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceResult<Guid>(ResultType.Success, newInvitation.InvitationKey);
    }
}
