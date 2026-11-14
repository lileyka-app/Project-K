using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.User;

/// <summary>Lets a suspended account sign in again. Sessions are not restored; the person signs in anew.</summary>
public record RestoreUserCommand(Guid TargetUserKey) : IRequest<ServiceResult<bool>>;

public class RestoreUserCommandHandler : IRequestHandler<RestoreUserCommand, ServiceResult<bool>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IActivityLogger _activityLogger;

    public RestoreUserCommandHandler(
        UserManager<AppUser> userManager,
        ICurrentUserContext currentUserContext,
        IActivityLogger activityLogger)
    {
        _userManager = userManager;
        _currentUserContext = currentUserContext;
        _activityLogger = activityLogger;
    }

    public async Task<ServiceResult<bool>> Handle(RestoreUserCommand request, CancellationToken cancellationToken)
    {
        var target = await _userManager.FindByIdAsync(request.TargetUserKey.ToString());
        if (target is null)
        {
            return ServiceResult<bool>.Failure(ResultType.NotFound, "UserNotFound", "Target user not found.");
        }

        if (target.CanSignIn())
        {
            return new ServiceResult<bool>(ResultType.Success, true);
        }

        target.OnboardingStatus = OnboardingStatus.Active;
        var update = await _userManager.UpdateAsync(target);
        if (!update.Succeeded)
        {
            return ServiceResult<bool>.Failure(ResultType.BadRequest, "RestoreFailed", "Failed to restore the account.");
        }

        _activityLogger.LogAudit(
            action: "Admin.UserRestored",
            actorUserId: _currentUserContext.UserId,
            targetUserId: target.Id,
            reason: "Administrator restored a suspended account.");

        return new ServiceResult<bool>(ResultType.Success, true);
    }
}
