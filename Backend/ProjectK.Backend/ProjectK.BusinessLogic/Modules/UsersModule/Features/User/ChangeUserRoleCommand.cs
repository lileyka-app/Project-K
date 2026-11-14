using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.User;

public record ChangeUserRoleCommand(Guid TargetUserId, UserRole NewRole) : IRequest<ServiceResult<Unit>>;

public class ChangeUserRoleCommandHandler : IRequestHandler<ChangeUserRoleCommand, ServiceResult<Unit>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IActivityLogger _activityLogger;
    private readonly IAccessChangeSignal _accessChanges;

    public ChangeUserRoleCommandHandler(
        UserManager<AppUser> userManager,
        ICurrentUserContext currentUserContext,
        IActivityLogger activityLogger,
        IAccessChangeSignal accessChanges)
    {
        _userManager = userManager;
        _currentUserContext = currentUserContext;
        _activityLogger = activityLogger;
        _accessChanges = accessChanges;
    }

    public async Task<ServiceResult<Unit>> Handle(ChangeUserRoleCommand request, CancellationToken cancellationToken)
    {
        // Get target user
        var targetUser = await _userManager.FindByIdAsync(request.TargetUserId.ToString());
        if (targetUser == null)
        {
            return UsersErrors.TargetNotFound<Unit>();
        }

        // Only admins manage the system Admin role. Kurin-level roles come from діловодські
        // offices (the Leadership screen) and are synced automatically, not set here.
        if (!_currentUserContext.IsAdmin())
        {
            return ServiceResult<Unit>.Failure(ResultType.Forbidden, UsersErrors.AdminOnly, "Only admins can change system roles.");
        }

        var currentRoles = await _userManager.GetRolesAsync(targetUser);
        var isCurrentlyAdmin = currentRoles.Contains(SystemRole.Admin, StringComparer.OrdinalIgnoreCase);

        if (request.NewRole == UserRole.Admin)
        {
            if (isCurrentlyAdmin)
            {
                return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
            }

            var addResult = await _userManager.AddToRoleAsync(targetUser, SystemRole.Admin);
            if (!addResult.Succeeded)
            {
                return ServiceResult<Unit>.Failure(ResultType.BadRequest, UsersErrors.AdminRoleNotGranted, "Failed to grant admin role.");
            }
        }
        else
        {
            if (isCurrentlyAdmin)
            {
                var removeResult = await _userManager.RemoveFromRoleAsync(targetUser, SystemRole.Admin);
                if (!removeResult.Succeeded)
                {
                    return ServiceResult<Unit>.Failure(ResultType.BadRequest, UsersErrors.AdminRoleNotRevoked, "Failed to revoke admin role.");
                }
            }

            if (!currentRoles.Contains(SystemRole.Member, StringComparer.OrdinalIgnoreCase))
            {
                await _userManager.AddToRoleAsync(targetUser, SystemRole.Member);
            }
        }

        _accessChanges.MarkUser(targetUser.Id);

        _activityLogger.LogAudit(
            action: "Admin.UserRoleChanged",
            actorUserId: _currentUserContext.UserId,
            targetUserId: targetUser.Id,
            reason: $"System role changed to {request.NewRole}.");

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
