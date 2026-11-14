using System.Net.Mail;
using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.UsersModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.UsersModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.Account;

public record ConfirmAccountEmailChangeCommand(Guid UserKey, string Email, string Token)
    : IRequest<ServiceResult<AccountSettingsDto>>;

public class ConfirmAccountEmailChangeCommandHandler : IRequestHandler<ConfirmAccountEmailChangeCommand, ServiceResult<AccountSettingsDto>>
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
        var user = await _userManager.FindByIdAsync(request.UserKey.ToString());
        if (user == null)
        {
            return UsersErrors.AccountMissing<AccountSettingsDto>();
        }

        var email = request.Email.Trim();
        if (!MailAddress.TryCreate(email, out _))
        {
            return ServiceResult<AccountSettingsDto>.Failure(ResultType.BadRequest, UsersErrors.InvalidEmail, "Invalid email format.");
        }

        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null && existingUser.Id != user.Id)
        {
            return ServiceResult<AccountSettingsDto>.Failure(ResultType.Conflict, UsersErrors.EmailAlreadyInUse, "Email is already in use.");
        }

        var changeEmailResult = await _userManager.ChangeEmailAsync(user, email, request.Token);
        if (!changeEmailResult.Succeeded)
        {
            return ServiceResult<AccountSettingsDto>.Failure(ResultType.BadRequest, UsersErrors.InvalidToken, "Invalid or expired email confirmation token.");
        }

        user.UserName = email;
        user.NormalizedUserName = _userManager.NormalizeName(email);
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return ServiceResult<AccountSettingsDto>.Failure(ResultType.BadRequest, UsersErrors.UpdateFailed, "Failed to update profile.");
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
