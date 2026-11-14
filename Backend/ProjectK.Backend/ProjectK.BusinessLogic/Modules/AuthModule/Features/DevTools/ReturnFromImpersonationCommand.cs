using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AuthModule.Models;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.DevTools;

/// <summary>
/// Steps the dev role switcher back out: the ticket names the administrator, the refresh tokens
/// are the borrowed session, which is ended so it does not outlive the test.
/// </summary>
public record ReturnFromImpersonationCommand(string Ticket, IReadOnlyList<string> RefreshTokens) : IRequest<ServiceResult<LoginUserResponse>>;

public class ReturnFromImpersonationCommandHandler : IRequestHandler<ReturnFromImpersonationCommand, ServiceResult<LoginUserResponse>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IJwtService _jwtService;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly ILoginResponseFactory _loginResponseFactory;
    private readonly IActivityLogger _activityLogger;

    public ReturnFromImpersonationCommandHandler(
        UserManager<AppUser> userManager,
        IJwtService jwtService,
        IRefreshTokenStore refreshTokens,
        ILoginResponseFactory loginResponseFactory,
        IActivityLogger activityLogger)
    {
        _userManager = userManager;
        _jwtService = jwtService;
        _refreshTokens = refreshTokens;
        _loginResponseFactory = loginResponseFactory;
        _activityLogger = activityLogger;
    }

    public async Task<ServiceResult<LoginUserResponse>> Handle(ReturnFromImpersonationCommand request, CancellationToken cancellationToken)
    {
        var adminKey = string.IsNullOrWhiteSpace(request.Ticket) ? null : _jwtService.ReadDevReturnTicket(request.Ticket);
        if (adminKey is null)
        {
            return ServiceResult<LoginUserResponse>.Failure(ResultType.Unauthorized, "InvalidTicket", "The return ticket is missing, expired or not one.");
        }

        var admin = await _userManager.FindByIdAsync(adminKey.Value.ToString());
        if (admin is null || !await _userManager.IsInRoleAsync(admin, SystemRole.Admin))
        {
            return ServiceResult<LoginUserResponse>.Failure(ResultType.Forbidden, "NotAnAdministrator", "The ticket does not belong to an administrator.");
        }

        foreach (var token in request.RefreshTokens.Distinct(StringComparer.Ordinal))
        {
            await _refreshTokens.RevokeAsync(token, cancellationToken);
        }

        var login = await _loginResponseFactory.CreateAsync(admin, cancellationToken);

        _activityLogger.LogAudit(
            action: "Dev.ImpersonateReturn",
            actorUserId: admin.Id,
            reason: "Dev role switcher: back to the administrator.");

        return new ServiceResult<LoginUserResponse>(ResultType.Success, login);
    }
}
