using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

/// <inheritdoc />
public sealed class AccountProvisioningService : IAccountProvisioningService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly RefreshTokenInvalidation _sessions;

    public AccountProvisioningService(
        UserManager<AppUser> userManager,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ICurrentUserContext currentUserContext,
        RefreshTokenInvalidation sessions)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _currentUserContext = currentUserContext;
        _sessions = sessions;
    }

    public async Task<AccountAvailability> CheckAvailabilityAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            return AccountAvailability.EmailTaken;
        }

        if (await _unitOfWork.WaitlistEntries.GetByEmailAsync(email, cancellationToken) is not null)
        {
            return AccountAvailability.WaitlistPending;
        }

        return AccountAvailability.Available;
    }

    public async Task<ServiceResult<AccountProvisioningResult>> ProvisionAsync(
        AccountProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            OnboardingStatus = OnboardingStatus.PendingActivation
        };

        var created = await _userManager.CreateAsync(user);
        if (!created.Succeeded)
        {
            var errors = string.Join(", ", created.Errors.Select(error => error.Description));
            return ServiceResult<AccountProvisioningResult>.Failure(
                ResultType.BadRequest,
                "UserNotCreated",
                $"Failed to create user: {errors}");
        }

        var waitlistEntryKey = request.WaitlistEntryKey ?? OpenApprovedWaitlistEntry(request, cancellationToken);

        var invitation = new Invitation
        {
            InvitationKey = Guid.NewGuid(),
            Token = Guid.NewGuid().ToString("N"),
            WaitlistEntryKey = waitlistEntryKey,
            TargetUserKey = user.Id,
            ExpiresAtUtc = _timeProvider.GetUtcNow().UtcDateTime.AddDays(OnboardingPolicy.InvitationLifetimeDays)
        };

        _unitOfWork.Invitations.Create(invitation, cancellationToken);

        return new ServiceResult<AccountProvisioningResult>(
            ResultType.Success,
            new AccountProvisioningResult(user.Id, invitation.InvitationKey, invitation.Token));
    }

    public async Task<AccountSnapshot?> FindAsync(Guid userKey, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userKey.ToString());
        return user is null ? null : new AccountSnapshot(user.Id, user.Email ?? string.Empty, user.OnboardingStatus);
    }

    public async Task<ServiceResult<AccountProvisioningResult>> ReissueInvitationAsync(
        Guid userKey,
        string email,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userKey.ToString());
        if (user is null)
        {
            return ServiceResult<AccountProvisioningResult>.Failure(ResultType.NotFound, "AccountNotFound", "Account not found.");
        }

        if (user.OnboardingStatus != OnboardingStatus.PendingActivation)
        {
            return ServiceResult<AccountProvisioningResult>.Failure(
                ResultType.Conflict, "AccountNotPending", "The account is already activated; only its owner can change its address.");
        }

        // The queue entry is found by the address the account was opened for, so it is looked up
        // before the account moves and then moved along with it — otherwise the old address would
        // keep holding a place in the queue and a resend by address would never find this person.
        var previousEmail = user.Email ?? string.Empty;
        var entry = await _unitOfWork.WaitlistEntries.GetByEmailAsync(previousEmail, cancellationToken);

        if (!string.Equals(previousEmail, email, StringComparison.OrdinalIgnoreCase))
        {
            if (await CheckAvailabilityAsync(email, cancellationToken) != AccountAvailability.Available)
            {
                return ServiceResult<AccountProvisioningResult>.Failure(
                    ResultType.Conflict, "EmailTaken", "This email is already used by another account or registration.");
            }

            // Identity saves the whole context here, so this runs before anything else is tracked:
            // the invitation below must still be pending when the caller commits.
            var moved = await MoveAsync(user, email);
            if (!moved.Succeeded)
            {
                return ServiceResult<AccountProvisioningResult>.Failure(
                    ResultType.BadRequest, "EmailNotChanged", string.Join(", ", moved.Errors.Select(error => error.Description)));
            }
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        Guid entryKey;
        if (entry is null)
        {
            entryKey = OpenApprovedWaitlistEntry(
                new AccountProvisioningRequest(email, user.FirstName, user.LastName, null),
                cancellationToken);
        }
        else
        {
            entry.Email = email;
            entry.InvitationSentAtUtc = now;
            _unitOfWork.WaitlistEntries.Update(entry, cancellationToken);
            entryKey = entry.WaitlistEntryKey;
        }

        // Revoked by account, not by entry: older tokens may hang off an entry this one replaced.
        var live = await _unitOfWork.Invitations.GetActiveForTargetUserAsync(user.Id, cancellationToken);
        foreach (var stale in live)
        {
            stale.IsRevoked = true;
            _unitOfWork.Invitations.Update(stale, cancellationToken);
        }

        var invitation = new Invitation
        {
            InvitationKey = Guid.NewGuid(),
            Token = Guid.NewGuid().ToString("N"),
            WaitlistEntryKey = entryKey,
            TargetUserKey = user.Id,
            ExpiresAtUtc = now.AddDays(OnboardingPolicy.InvitationLifetimeDays)
        };

        _unitOfWork.Invitations.Create(invitation, cancellationToken);

        return new ServiceResult<AccountProvisioningResult>(
            ResultType.Success,
            new AccountProvisioningResult(user.Id, invitation.InvitationKey, invitation.Token));
    }

    public async Task<ServiceResult<Guid>> ChangeEmailAsync(
        Guid userKey,
        string email,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userKey.ToString());
        if (user is null)
        {
            return ServiceResult<Guid>.Failure(ResultType.NotFound, "AccountNotFound", "Account not found.");
        }

        if (string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            return new ServiceResult<Guid>(ResultType.Success, user.Id);
        }

        var holder = await _userManager.FindByEmailAsync(email);
        if (holder is not null && holder.Id != user.Id)
        {
            return ServiceResult<Guid>.Failure(ResultType.Conflict, "EmailTaken", "This email is already used by another account.");
        }

        var moved = await MoveAsync(user, email);
        if (!moved.Succeeded)
        {
            return ServiceResult<Guid>.Failure(
                ResultType.BadRequest, "EmailNotChanged", string.Join(", ", moved.Errors.Select(error => error.Description)));
        }

        // The address is what a password reset goes to, so a session opened under the old one
        // must not outlive the change.
        await _sessions.RevokeRefreshTokenAsync(user, cancellationToken);
        return new ServiceResult<Guid>(ResultType.Success, user.Id);
    }

    /// <summary>The address is also the user name; Identity normalizes both on update.</summary>
    private Task<IdentityResult> MoveAsync(AppUser user, string email)
    {
        user.Email = email;
        user.UserName = email;
        return _userManager.UpdateAsync(user);
    }

    /// <summary>
    /// Records the queue entry an invitation has to hang off for someone who never queued: a провід
    /// added them directly, so the entry is written already approved, by whoever is asking.
    /// </summary>
    private Guid OpenApprovedWaitlistEntry(AccountProvisioningRequest request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var entry = new WaitlistEntry
        {
            WaitlistEntryKey = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber ?? string.Empty,
            DateOfBirth = (request.DateOfBirth ?? DateOnly.FromDateTime(now)).ToDateTime(TimeOnly.MinValue),
            IsKurinLeaderCandidate = false,
            Source = WaitlistEntrySource.DirectAdd,
            VerificationStatus = WaitlistVerificationStatus.ApprovedForInvitation,
            RequestedAtUtc = now,
            ReviewedAtUtc = now,
            ApprovedAtUtc = now,
            ReviewedByUserKey = _currentUserContext.UserId,
            InvitationSentAtUtc = now
        };

        _unitOfWork.WaitlistEntries.Create(entry, cancellationToken);
        return entry.WaitlistEntryKey;
    }
}
