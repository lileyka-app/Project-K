using System.Net.Mail;
using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.IdentityModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.IdentityModule;
using ProjectK.Common.Records.Shared;

using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.Account;

public sealed record ConfirmAccountEmailChangeCommand(Guid UserKey, string Email, string Token)
    : IRequest<ServiceResult<AccountSettingsDto>>;

public sealed class ConfirmAccountEmailChangeCommandHandler : IRequestHandler<ConfirmAccountEmailChangeCommand, ServiceResult<AccountSettingsDto>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RefreshTokenInvalidation _sessions;
    private readonly IDomainEventPublisher _events;
    private readonly IMediator _mediator;
    private readonly IActivityLogger _activityLogger;

    public ConfirmAccountEmailChangeCommandHandler(
        UserManager<AppUser> userManager,
        IDomainEventPublisher events,
        IMediator mediator,
        IActivityLogger activityLogger,
        RefreshTokenInvalidation sessions)
    {
        _userManager = userManager;
        _events = events;
        _mediator = mediator;
        _activityLogger = activityLogger;
        _sessions = sessions;
    }

    public async Task<ServiceResult<AccountSettingsDto>> Handle(ConfirmAccountEmailChangeCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindSignedInAsync(request.UserKey);
        if (user == null)
        {
            return IdentityErrors.AccountMissing<AccountSettingsDto>();
        }

        var email = request.Email.Trim();
        if (!MailAddress.TryCreate(email, out _))
        {
            return ServiceResult<AccountSettingsDto>.Failure(ResultType.BadRequest, IdentityErrors.InvalidEmail, "Invalid email format.");
        }

        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null && existingUser.Id != user.Id)
        {
            return ServiceResult<AccountSettingsDto>.Failure(ResultType.Conflict, IdentityErrors.EmailAlreadyInUse, "Email is already in use.");
        }

        var changeEmailResult = await _userManager.ChangeEmailAsync(user, email, request.Token);
        if (!changeEmailResult.Succeeded)
        {
            return ServiceResult<AccountSettingsDto>.Failure(ResultType.BadRequest, IdentityErrors.InvalidToken, "Invalid or expired email confirmation token.");
        }

        user.UserName = email;
        user.NormalizedUserName = _userManager.NormalizeName(email);
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return ServiceResult<AccountSettingsDto>.Failure(ResultType.BadRequest, IdentityErrors.UpdateFailed, "Failed to update profile.");
        }

        // Only once the change is stored. RevokeAllAsync commits on its own, so ending the
        // sessions first signed every device out even when this update failed.
        await _sessions.RevokeRefreshTokenAsync(user, cancellationToken);

        _activityLogger.LogAudit(
            action: "Account.EmailChangeConfirmed",
            actorUserId: user.Id,
            newEmail: email,
            reason: "Email change confirmed.");

        await _events.PublishAsync(new AccountEmailChanged(user.Id, email), cancellationToken);

        return await _mediator.Send(new GetAccountSettingsQuery(user.Id), cancellationToken);
    }
}
