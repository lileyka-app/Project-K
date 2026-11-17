using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ProjectK.BusinessLogic.Behaviors;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.Onboarding;

public sealed record ResendInvitationByEmailCommand(string Email) : IRequest<ServiceResult<Unit>>, ITransactionalRequest;

public sealed class ResendInvitationByEmailCommandValidator : AbstractValidator<ResendInvitationByEmailCommand>
{
    public ResendInvitationByEmailCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress();
    }
}

public sealed class ResendInvitationByEmailCommandHandler : IRequestHandler<ResendInvitationByEmailCommand, ServiceResult<Unit>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ResendInvitationByEmailCommandHandler> _logger;

    public ResendInvitationByEmailCommandHandler(
        UserManager<AppUser> userManager,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        TimeProvider timeProvider,
        ILogger<ResendInvitationByEmailCommandHandler> logger)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ServiceResult<Unit>> Handle(
        ResendInvitationByEmailCommand request,
        CancellationToken cancellationToken)
    {
        // Every exit from here answers the same, so each one says in the log which it was. Without
        // that, a request that quietly sent nothing reads exactly like one that sent a letter,
        // which is how this endpoint looked healthy while helping nobody.
        //
        // What none of these lines carry is who: no address, no account key, no status of one.
        // This endpoint exists to refuse to tell a stranger whether an address is known, and
        // writing that same answer into the log would only move the disclosure, not avoid it.
        // The request's own trace id is what ties a line back to an attempt.
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            _logger.LogInformation("An invitation resend was asked for an address no account holds; nothing was sent.");
            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
        }

        if (user.OnboardingStatus != OnboardingStatus.PendingActivation)
        {
            _logger.LogInformation("An invitation resend was asked for an account that is not awaiting activation; nothing was sent.");
            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var entry = await _unitOfWork.WaitlistEntries.GetByEmailAsync(user.Email!, cancellationToken);
        var entryIsNew = entry is null;

        // An invitation hangs off a queue entry, and retention deleted that entry out from under
        // accounts still waiting to activate — taking their invitations with it, since the key
        // cascades. Rebuilding the entry is what lets such a person be invited at all.
        entry ??= OpenApprovedWaitlistEntry(user, now);
        if (entryIsNew)
        {
            _logger.LogWarning(
                "An account awaiting activation had no waitlist entry for an invitation to hang off; one was rebuilt from the account.");
        }

        var newInvitation = new Invitation
        {
            InvitationKey = Guid.NewGuid(),
            Token = Guid.NewGuid().ToString("N"),
            WaitlistEntryKey = entry.WaitlistEntryKey,
            TargetUserKey = user.Id,
            ExpiresAtUtc = now.AddDays(OnboardingRules.InvitationLifetimeDays)
        };

        // The letter goes first, and nothing is written until it is away. A send that throws must
        // leave the account exactly as it was: the invitation they already hold keeps working, and
        // no second live token is left behind for the next resend to pick between.
        try
        {
            await _emailService.SendInvitationEmailAsync(user.Email!, newInvitation.Token, cancellationToken);
        }
        catch (Exception exception)
        {
            // Answering differently here would say the address is one we know. The caller is told
            // the same thing an unknown address is told, and the failure is left in the log.
            _logger.LogError(exception, "Could not send a replacement invitation; nothing was changed for the account.");

            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
        }

        if (entryIsNew)
        {
            _unitOfWork.WaitlistEntries.Create(entry, cancellationToken);
        }
        else
        {
            entry.InvitationSentAtUtc = now;
            _unitOfWork.WaitlistEntries.Update(entry, cancellationToken);
        }

        // One save: a person is never left holding two live invitations. Revoking by account
        // rather than by entry matters for anyone whose older tokens hang off an entry that is no
        // longer the one their address finds.
        var live = await _unitOfWork.Invitations.GetActiveForTargetUserAsync(user.Id, cancellationToken);
        foreach (var invitation in live)
        {
            invitation.IsRevoked = true;
            _unitOfWork.Invitations.Update(invitation, cancellationToken);
        }

        _unitOfWork.Invitations.Create(newInvitation, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("A replacement invitation was sent.");

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }

    /// <summary>
    /// The queue entry an invitation has to hang off, rebuilt for an account whose own entry is
    /// gone. It is written already approved because the account it describes was approved once:
    /// this restores the record of that, and never creates an account.
    /// </summary>
    private static WaitlistEntry OpenApprovedWaitlistEntry(AppUser user, DateTime now) => new()
    {
        WaitlistEntryKey = Guid.NewGuid(),
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email!,
        PhoneNumber = user.PhoneNumber ?? string.Empty,
        DateOfBirth = DateTime.UnixEpoch,
        IsKurinLeaderCandidate = false,
        VerificationStatus = WaitlistVerificationStatus.ApprovedForInvitation,
        RequestedAtUtc = now,
        ReviewedAtUtc = now,
        ApprovedAtUtc = now,
        InvitationSentAtUtc = now
    };
}
