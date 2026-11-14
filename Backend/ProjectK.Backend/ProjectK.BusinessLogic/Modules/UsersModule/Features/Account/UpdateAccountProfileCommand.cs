using System.Net;
using System.Net.Mail;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.UsersModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.UsersModule;
using ProjectK.Common.Options.InfrastructureModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.Account;

public record UpdateAccountProfileCommand(Guid UserKey, string Email, string? PhoneNumber, string? CurrentPassword = null)
    : IRequest<ServiceResult<AccountSettingsDto>>;

public class UpdateAccountProfileCommandHandler : IRequestHandler<UpdateAccountProfileCommand, ServiceResult<AccountSettingsDto>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IDomainEventPublisher _events;
    private readonly IMediator _mediator;
    private readonly IEmailService _emailService;
    private readonly EmailOptions _emailSettings;
    private readonly IActivityLogger _activityLogger;

    public UpdateAccountProfileCommandHandler(
        UserManager<AppUser> userManager,
        IDomainEventPublisher events,
        IMediator mediator,
        IEmailService emailService,
        IOptions<EmailOptions> emailSettings,
        IActivityLogger activityLogger)
    {
        _userManager = userManager;
        _events = events;
        _mediator = mediator;
        _emailService = emailService;
        _emailSettings = emailSettings.Value;
        _activityLogger = activityLogger;
    }

    public async Task<ServiceResult<AccountSettingsDto>> Handle(UpdateAccountProfileCommand request, CancellationToken cancellationToken)
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

        var currentEmail = user.Email ?? string.Empty;
        var emailChanged = !string.Equals(currentEmail, email, StringComparison.OrdinalIgnoreCase);
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null && existingUser.Id != user.Id)
        {
            return ServiceResult<AccountSettingsDto>.Failure(ResultType.Conflict, UsersErrors.EmailAlreadyInUse, "Email is already in use.");
        }

        if (emailChanged)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            {
                return ServiceResult<AccountSettingsDto>.Failure(ResultType.Unauthorized, UsersErrors.InvalidCredentials, "Current password is required to change email.");
            }

            var passwordValid = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
            if (!passwordValid)
            {
                return ServiceResult<AccountSettingsDto>.Failure(ResultType.Unauthorized, UsersErrors.InvalidCredentials, "Invalid current password.");
            }
        }

        var phone = PhoneNumbers.Normalize(request.PhoneNumber);
        if (phone is null)
        {
            return ServiceResult<AccountSettingsDto>.Failure(ResultType.BadRequest, PhoneNumbers.ErrorCode, PhoneNumbers.InvalidMessage);
        }

        user.PhoneNumber = phone.Length == 0 ? null : phone;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return ServiceResult<AccountSettingsDto>.Failure(ResultType.BadRequest, UsersErrors.UpdateFailed, "Failed to update profile.");
        }

        await _events.PublishAsync(new AccountPhoneChanged(user.Id, user.PhoneNumber ?? string.Empty), cancellationToken);

        var settingsResult = await _mediator.Send(new GetAccountSettingsQuery(user.Id), cancellationToken);
        if (!emailChanged || settingsResult.Data == null)
        {
            return settingsResult;
        }

        var token = await _userManager.GenerateChangeEmailTokenAsync(user, email);
        var confirmationUrl = BuildEmailChangeConfirmationUrl(email, token);

        _activityLogger.LogAudit(
            action: "Account.EmailChangeRequested",
            actorUserId: user.Id,
            email: currentEmail,
            newEmail: email,
            reason: "Email change requested.");

        await _emailService.SendEmailChangeConfirmationEmailAsync(email, currentEmail, confirmationUrl, cancellationToken);

        return new ServiceResult<AccountSettingsDto>(
            ResultType.Success,
            settingsResult.Data with { PendingEmail = email });
    }

    private string BuildEmailChangeConfirmationUrl(string email, string token)
    {
        var baseUrl = _emailSettings.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/settings/account?confirmEmail=true&email={WebUtility.UrlEncode(email)}&token={WebUtility.UrlEncode(token)}";
    }
}
