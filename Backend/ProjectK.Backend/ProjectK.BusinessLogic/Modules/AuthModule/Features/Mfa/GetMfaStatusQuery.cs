using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos.AuthModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Mfa;

/// <summary>
/// Whether the signed-in account has a second factor, and whether it has to. Enforcement is a
/// policy decision, not an account setting: holding a privileged office can make the second factor
/// mandatory for an account that had it switched off.
/// </summary>
public sealed record GetMfaStatusQuery : IRequest<ServiceResult<MfaStatusDto>>;

public sealed class GetMfaStatusQueryHandler : IRequestHandler<GetMfaStatusQuery, ServiceResult<MfaStatusDto>>
{
    private readonly ICurrentUserContext _currentUser;
    private readonly UserManager<AppUser> _userManager;
    private readonly IMfaEnforcementPolicy _policy;

    public GetMfaStatusQueryHandler(
        ICurrentUserContext currentUser,
        UserManager<AppUser> userManager,
        IMfaEnforcementPolicy policy)
    {
        _currentUser = currentUser;
        _userManager = userManager;
        _policy = policy;
    }

    public async Task<ServiceResult<MfaStatusDto>> Handle(GetMfaStatusQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userKey)
        {
            return AuthErrors.AccountMissing<MfaStatusDto>();
        }

        var user = await _userManager.FindByIdAsync(userKey.ToString());
        if (user is null)
        {
            return ServiceResult<MfaStatusDto>.Failure(ResultType.NotFound, AuthErrors.UserNotFound, "User not found.");
        }

        var isPrivileged = RolePermissionMap.GrantsWholeKurinManagement(_currentUser.Roles ?? []);
        var isMfaRequired = isPrivileged && await _policy.IsPrivilegedMfaRequiredAsync(cancellationToken);

        return new ServiceResult<MfaStatusDto>(
            ResultType.Success,
            new MfaStatusDto(user.TwoFactorEnabled, isMfaRequired));
    }
}
