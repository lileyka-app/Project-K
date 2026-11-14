using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Dtos.AuthModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuthModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.DevTools;

/// <summary>
/// Steps the dev role switcher back out: the ticket names the administrator, the refresh tokens
/// are the borrowed session, which is ended so it does not outlive the test.
/// </summary>
public record ReturnFromImpersonationCommand(string Ticket, IReadOnlyList<string> RefreshTokens) : IRequest<ServiceResult<LoginUserDto>>;

public class ReturnFromImpersonationCommandHandler : IRequestHandler<ReturnFromImpersonationCommand, ServiceResult<LoginUserDto>>
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

    public async Task<ServiceResult<LoginUserDto>> Handle(ReturnFromImpersonationCommand request, CancellationToken cancellationToken)
    {
        var adminKey = string.IsNullOrWhiteSpace(request.Ticket) ? null : _jwtService.ReadDevReturnTicket(request.Ticket);
        if (adminKey is null)
        {
            return ServiceResult<LoginUserDto>.Failure(ResultType.Unauthorized, AuthErrors.InvalidTicket, "The return ticket is missing, expired or not one.");
        }

        var admin = await _userManager.FindByIdAsync(adminKey.Value.ToString());
        if (admin is null || !await _userManager.IsInRoleAsync(admin, SystemRole.Admin))
        {
            return ServiceResult<LoginUserDto>.Failure(ResultType.Forbidden, AuthErrors.NotAnAdministrator, "The ticket does not belong to an administrator.");
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

        return new ServiceResult<LoginUserDto>(ResultType.Success, login);
    }
}
