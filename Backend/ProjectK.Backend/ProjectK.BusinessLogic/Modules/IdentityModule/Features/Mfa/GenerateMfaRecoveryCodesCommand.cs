using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Dtos.IdentityModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.Mfa;

public sealed record GenerateMfaRecoveryCodesCommand(Guid UserKey, string CurrentPassword)
    : IRequest<ServiceResult<MfaRecoveryCodesResponseDto>>;

public sealed class GenerateMfaRecoveryCodesCommandHandler
    : IRequestHandler<GenerateMfaRecoveryCodesCommand, ServiceResult<MfaRecoveryCodesResponseDto>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IActivityLogger _activityLogger;

    public GenerateMfaRecoveryCodesCommandHandler(
        UserManager<AppUser> userManager,
        IActivityLogger activityLogger)
    {
        _userManager = userManager;
        _activityLogger = activityLogger;
    }

    public async Task<ServiceResult<MfaRecoveryCodesResponseDto>> Handle(
        GenerateMfaRecoveryCodesCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindSignedInAsync(request.UserKey);
        if (user == null)
        {
            return IdentityErrors.AccountMissing<MfaRecoveryCodesResponseDto>();
        }

        if (!user.TwoFactorEnabled)
        {
            return ServiceResult<MfaRecoveryCodesResponseDto>.Failure(ResultType.BadRequest, IdentityErrors.MfaNotEnabled, "MFA is not enabled.");
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword)
            || !await _userManager.CheckPasswordAsync(user, request.CurrentPassword))
        {
            return ServiceResult<MfaRecoveryCodesResponseDto>.Failure(ResultType.Unauthorized, IdentityErrors.InvalidCredentials, "Invalid credentials.");
        }

        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

        _activityLogger.LogAudit(
            action: "Account.MfaRecoveryCodesRotated",
            actorUserId: user.Id,
            targetUserId: user.Id,
            reason: "User rotated/generated MFA recovery codes.");

        return new ServiceResult<MfaRecoveryCodesResponseDto>(
            ResultType.Success,
            new MfaRecoveryCodesResponseDto(recoveryCodes ?? Enumerable.Empty<string>()));
    }
}
