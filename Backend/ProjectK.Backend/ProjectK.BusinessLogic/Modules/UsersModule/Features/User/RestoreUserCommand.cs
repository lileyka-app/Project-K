using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuthModule;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.User;

/// <summary>Lets a suspended account sign in again. Sessions are not restored; the person signs in anew.</summary>
public sealed record RestoreUserCommand(Guid TargetUserKey) : IRequest<ServiceResult<Unit>>;

public sealed class RestoreUserCommandHandler : IRequestHandler<RestoreUserCommand, ServiceResult<Unit>>
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

    public async Task<ServiceResult<Unit>> Handle(RestoreUserCommand request, CancellationToken cancellationToken)
    {
        var target = await _userManager.FindByIdAsync(request.TargetUserKey.ToString());
        if (target is null)
        {
            return UsersErrors.TargetNotFound<Unit>();
        }

        if (target.CanSignIn())
        {
            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
        }

        target.OnboardingStatus = OnboardingStatus.Active;
        var update = await _userManager.UpdateAsync(target);
        if (!update.Succeeded)
        {
            return ServiceResult<Unit>.Failure(ResultType.BadRequest, UsersErrors.RestoreFailed, "Failed to restore the account.");
        }

        _activityLogger.LogAudit(
            action: "Admin.UserRestored",
            actorUserId: _currentUserContext.UserId,
            targetUserId: target.Id,
            reason: "Administrator restored a suspended account.");

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
