using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AuthModule.Models;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership.Upsert;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership.Join;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding;

/// <summary>
/// Turns an invitation into a usable account and answers with a signed-in session, so the person
/// lands inside the app rather than on the sign-in form they have never used.
/// </summary>
public record ActivateAccountCommand(string Token, string Password) : IRequest<ServiceResult<LoginUserResponse>>;

public sealed class ActivateAccountCommandValidator : AbstractValidator<ActivateAccountCommand>
{
    public ActivateAccountCommandValidator()
    {
        // Presence only; password complexity stays with the Identity password policy.
        RuleFor(command => command.Token).NotEmpty();
        RuleFor(command => command.Password).NotEmpty();
    }
}

public class ActivateAccountCommandHandler : IRequestHandler<ActivateAccountCommand, ServiceResult<LoginUserResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly UserManager<AppUser> _userManager;
    private readonly IMediator _mediator;
    private readonly TimeProvider _timeProvider;
    private readonly ILoginResponseFactory _loginResponseFactory;

    public ActivateAccountCommandHandler(IUnitOfWork unitOfWork, IMemberDirectory members, UserManager<AppUser> userManager, IMediator mediator, TimeProvider timeProvider, ILoginResponseFactory loginResponseFactory)
    {
        _loginResponseFactory = loginResponseFactory;
        _unitOfWork = unitOfWork;
        _members = members;
        _userManager = userManager;
        _mediator = mediator;
        _timeProvider = timeProvider;
    }

    public async Task<ServiceResult<LoginUserResponse>> Handle(ActivateAccountCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate Token
        var invitation = await _unitOfWork.Invitations.GetByTokenAsync(request.Token, cancellationToken);

        if (invitation == null || invitation.ExpiresAtUtc < _timeProvider.GetUtcNow().UtcDateTime)
        {
            return ServiceResult<LoginUserResponse>.Failure(ResultType.BadRequest, "InvalidInvitationToken", "Invalid or expired invitation token.");
        }

        // 2. Get User
        if (!invitation.TargetUserKey.HasValue)
        {
            return ServiceResult<LoginUserResponse>.Failure(ResultType.BadRequest, "InvitationHasNoUser", "Invitation is not linked to a user.");
        }

        var user = await _userManager.FindByIdAsync(invitation.TargetUserKey.Value.ToString());
        if (user == null)
        {
            return ServiceResult<LoginUserResponse>.Failure(ResultType.NotFound, "UserNotFound", "Target user not found.");
        }

        // 3. Set Password and Activate
        var addPasswordResult = await _userManager.AddPasswordAsync(user, request.Password);
        if (!addPasswordResult.Succeeded)
        {
            var errors = string.Join(", ", addPasswordResult.Errors.Select(e => e.Description));
            return ServiceResult<LoginUserResponse>.Failure(ResultType.BadRequest, "PasswordNotSet", $"Failed to set password: {errors}");
        }

        user.OnboardingStatus = OnboardingStatus.Active;
        user.EmailConfirmed = true; // Invitation activation implies email verification
        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
            return ServiceResult<LoginUserResponse>.Failure(ResultType.BadRequest, "UserNotUpdated", $"Failed to update user status: {errors}");
        }

        // 3.5. Assign the baseline system role. Kurin authority comes from a діловодський office,
        // created below for a kurin-leader candidate.
        var entry = await _unitOfWork.WaitlistEntries.GetByKeyAsync(invitation.WaitlistEntryKey, cancellationToken);
        bool isKurinLeaderCandidate = entry?.IsKurinLeaderCandidate ?? false;

        await _userManager.AddToRoleAsync(user, SystemRole.Member);

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
        var memberKey = await _members.EnsureForAccountAsync(
            new MemberForAccount(
                user.Id,
                user.Email!,
                user.FirstName,
                user.LastName,
                entry?.PhoneNumber ?? string.Empty,
                entry != null ? DateOnly.FromDateTime(entry.DateOfBirth) : new DateOnly(2000, 1, 1),
                KurinKey: Guid.Empty),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 5.5. A founder is taken into the kurin their approval opened for them. The queue entry
        // is what remembers that kurin, and here it is the only thing that can: the kurin was
        // created moments ago and nobody belongs to it yet. Joining rather than placing matters —
        // someone who already belongs to a kurin keeps it and founds the new one alongside.
        var foundedKurinKey = entry?.FoundedKurinKey;
        if (foundedKurinKey.HasValue && foundedKurinKey.Value != Guid.Empty)
        {
            await _mediator.Send(
                new JoinKurinCommand(memberKey, foundedKurinKey.Value, GroupKey: null),
                cancellationToken);
        }

        // 6. A kurin-leader candidate becomes the kurin's Зв'язковий; the office sync grants the role.
        if (isKurinLeaderCandidate && foundedKurinKey.HasValue && foundedKurinKey.Value != Guid.Empty)
        {
            await _mediator.Send(new UpsertLeadershipCommand(new UpsertLeadershipRequest
            {
                Type = LeadershipType.KV.ToString(),
                EntityKey = foundedKurinKey.Value,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                LeadershipHistories = new[]
                {
                    new LeadershipHistoryMemberDto
                    {
                        Role = LeadershipRole.Zvyazkovyi.ToString(),
                        StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                        Member = new MemberLookupDto { MemberKey = memberKey }
                    }
                }
            })
            {
                // The activating user is still anonymous here, so there is no assigner to check.
                SeatedBySystem = true
            }, cancellationToken);
        }

        // Signed in on the spot: the password was just chosen, asking for it again is a wall.
        var session = await _loginResponseFactory.CreateAsync(user, cancellationToken);
        return new ServiceResult<LoginUserResponse>(ResultType.Success, session);
    }
}
