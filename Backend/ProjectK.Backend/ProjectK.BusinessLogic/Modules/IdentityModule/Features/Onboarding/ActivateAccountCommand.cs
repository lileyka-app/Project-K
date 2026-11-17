using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.IdentityModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.IdentityModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.Onboarding;

/// <summary>
/// Turns an invitation into a usable account and answers with a signed-in session, so the person
/// lands inside the app rather than on the sign-in form they have never used.
/// </summary>
public sealed record ActivateAccountCommand(string Token, string Password) : IRequest<ServiceResult<LoginUserDto>>;

public sealed class ActivateAccountCommandValidator : AbstractValidator<ActivateAccountCommand>
{
    public ActivateAccountCommandValidator()
    {
        // Presence only; password complexity stays with the Identity password policy.
        RuleFor(command => command.Token).NotEmpty();
        RuleFor(command => command.Password).NotEmpty();
    }
}

public sealed class ActivateAccountCommandHandler : IRequestHandler<ActivateAccountCommand, ServiceResult<LoginUserDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly UserManager<AppUser> _userManager;
    private readonly IKurinFounderWriter _activation;
    private readonly TimeProvider _timeProvider;
    private readonly ILoginResponseService _loginResponseService;
    private readonly IDomainEventPublisher _events;

    public ActivateAccountCommandHandler(IUnitOfWork unitOfWork, IMemberDirectory members, UserManager<AppUser> userManager, IKurinFounderWriter activation, TimeProvider timeProvider, ILoginResponseService loginResponseService, IDomainEventPublisher events)
    {
        _loginResponseService = loginResponseService;
        _events = events;
        _unitOfWork = unitOfWork;
        _members = members;
        _userManager = userManager;
        _activation = activation;
        _timeProvider = timeProvider;
    }

    public async Task<ServiceResult<LoginUserDto>> Handle(ActivateAccountCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate Token
        var invitation = await _unitOfWork.Invitations.GetByTokenAsync(request.Token, cancellationToken);

        if (invitation == null || invitation.ExpiresAtUtc < _timeProvider.GetUtcNow().UtcDateTime)
        {
            return ServiceResult<LoginUserDto>.Failure(ResultType.BadRequest, IdentityErrors.InvalidInvitationToken, "Invalid or expired invitation token.");
        }

        // 2. Get User
        if (!invitation.TargetUserKey.HasValue)
        {
            return ServiceResult<LoginUserDto>.Failure(ResultType.BadRequest, IdentityErrors.InvitationHasNoUser, "Invitation is not linked to a user.");
        }

        var user = await _userManager.FindByIdAsync(invitation.TargetUserKey.Value.ToString());
        if (user == null)
        {
            return ServiceResult<LoginUserDto>.Failure(ResultType.NotFound, IdentityErrors.UserNotFound, "Target user not found.");
        }

        // 3. Set Password and Activate
        var addPasswordResult = await _userManager.AddPasswordAsync(user, request.Password);
        if (!addPasswordResult.Succeeded)
        {
            var errors = string.Join(", ", addPasswordResult.Errors.Select(e => e.Description));
            return ServiceResult<LoginUserDto>.Failure(ResultType.BadRequest, IdentityErrors.PasswordNotSet, $"Failed to set password: {errors}");
        }

        user.OnboardingStatus = OnboardingStatus.Active;
        user.EmailConfirmed = true; // Invitation activation implies email verification
        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
            return ServiceResult<LoginUserDto>.Failure(ResultType.BadRequest, IdentityErrors.UserNotUpdated, $"Failed to update user status: {errors}");
        }

        // 3.5. Assign the baseline system role. Kurin authority comes from a діловодський office,
        // created below for a kurin-leader candidate.
        var entry = await _unitOfWork.WaitlistEntries.GetByKeyAsync(invitation.WaitlistEntryKey, cancellationToken);
        bool isKurinLeaderCandidate = entry?.IsKurinLeaderCandidate ?? false;

        await _userManager.AddToRoleAsync(user, SystemRoleNames.Member);

        // 4. Mark Invitation as used, and the queue entry as one nobody is waiting on any more.
        // The entry keeps the only record of how this account came to be; leaving it looking
        // exactly like an entry still waiting is what let retention mistake the two.
        invitation.UsedAtUtc = DateTime.UtcNow;
        _unitOfWork.Invitations.Update(invitation, cancellationToken);

        // The entry is marked accepted so retention can tell it from one still waiting.
        if (entry is not null)
        {
            entry.InvitationAcceptedAtUtc = DateTime.UtcNow;
            _unitOfWork.WaitlistEntries.Update(entry, cancellationToken);
        }

        // 5. Make sure the account has a member, linking an existing one when the address is known.
        // The member module owns that decision — this handler no longer writes Member rows itself.
        // Published before the save, so the person commits with the activation.
        await _events.PublishAsync(
            new AccountActivated(new MemberForAccount(
                user.Id,
                user.Email!,
                user.FirstName,
                user.LastName,
                entry?.PhoneNumber ?? string.Empty,
                entry != null ? DateOnly.FromDateTime(entry.DateOfBirth) : new DateOnly(2000, 1, 1),
                KurinKey: Guid.Empty)),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var memberKey = (await _members.FindByAccountAsync(user.Id, cancellationToken))!.MemberKey;

        // 5.5. A founder is taken into the kurin their approval opened for them. The queue entry
        // is what remembers that kurin, and here it is the only thing that can: the kurin was
        // created moments ago and nobody belongs to it yet.
        var foundedKurinKey = entry?.FoundedKurinKey;
        if (foundedKurinKey.HasValue && foundedKurinKey.Value != Guid.Empty)
        {
            await _activation.JoinFoundedKurinAsync(memberKey, foundedKurinKey.Value, cancellationToken);
        }

        // 6. A kurin-leader candidate becomes the kurin's Зв'язковий; the office sync grants the role.
        if (isKurinLeaderCandidate && foundedKurinKey.HasValue && foundedKurinKey.Value != Guid.Empty)
        {
            await _activation.SeatFounderAsync(memberKey, foundedKurinKey.Value, cancellationToken);
        }

        // Signed in on the spot: the password was just chosen, asking for it again is a wall.
        var session = await _loginResponseService.CreateAsync(user, cancellationToken);
        return new ServiceResult<LoginUserDto>(ResultType.Success, session);
    }
}
