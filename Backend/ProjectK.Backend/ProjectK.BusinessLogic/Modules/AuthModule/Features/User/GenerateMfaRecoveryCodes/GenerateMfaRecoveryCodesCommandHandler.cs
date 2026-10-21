using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.AuthModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.User.GenerateMfaRecoveryCodes;

public class GenerateMfaRecoveryCodesCommandHandler
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
        var user = await _userManager.FindByIdAsync(request.UserKey.ToString());
        if (user == null)
        {
            return ServiceResult<MfaRecoveryCodesResponseDto>.Failure(ResultType.Unauthorized, "Unauthorized", "User not found or unauthorized.");
        }

        if (!user.TwoFactorEnabled)
        {
            return ServiceResult<MfaRecoveryCodesResponseDto>.Failure(ResultType.BadRequest, "MfaNotEnabled", "MFA is not enabled.");
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword)
            || !await _userManager.CheckPasswordAsync(user, request.CurrentPassword))
        {
            return ServiceResult<MfaRecoveryCodesResponseDto>.Failure(ResultType.Unauthorized, "InvalidCredentials", "Invalid credentials.");
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
