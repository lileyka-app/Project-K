using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.AuthModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Security;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.LoadTest;

/// <summary>
/// Mints a token for the load-test account without a password. Guarded by <c>LoadTestLoginKey</c>,
/// which ships blank — a blank key disables the whole thing. It has its own secret rather than
/// sharing the rate limiter's bypass key, so letting a monitor past the limiter cannot also open
/// a login.
/// </summary>
public sealed record LoadTestLoginCommand(string ApiKey) : IRequest<ServiceResult<LoadTestSessionDto>>;

public sealed class LoadTestLoginCommandHandler : IRequestHandler<LoadTestLoginCommand, ServiceResult<LoadTestSessionDto>>
{
    public const string LoadTestAccountEmail = "loadtest@projectk.com";

    private readonly IConfiguration _configuration;
    private readonly UserManager<AppUser> _userManager;
    private readonly IJwtService _jwtService;
    private readonly IAccessContextResolver _access;

    public LoadTestLoginCommandHandler(
        IConfiguration configuration,
        UserManager<AppUser> userManager,
        IJwtService jwtService,
        IAccessContextResolver access)
    {
        _configuration = configuration;
        _userManager = userManager;
        _jwtService = jwtService;
        _access = access;
    }

    public async Task<ServiceResult<LoadTestSessionDto>> Handle(LoadTestLoginCommand request, CancellationToken cancellationToken)
    {
        var expectedKey = _configuration["LoadTestLoginKey"];
        if (string.IsNullOrEmpty(expectedKey) || !SecretComparer.Matches(request.ApiKey, expectedKey))
        {
            return ServiceResult<LoadTestSessionDto>.Failure(
                ResultType.Unauthorized, AuthErrors.InvalidApiKey, "Invalid or disabled load test API key.");
        }

        var user = await _userManager.FindByEmailAsync(LoadTestAccountEmail);
        if (user is null)
        {
            return ServiceResult<LoadTestSessionDto>.Failure(ResultType.NotFound, AuthErrors.UserNotFound, "Load test user not found.");
        }

        var context = await _access.ResolveAsync(user, cancellationToken);
        var token = _jwtService.GenerateAccessToken(
            user.Id.ToString(), user.Email!, context.Roles, context.KurinKey?.ToString(), user.TwoFactorEnabled);

        return new ServiceResult<LoadTestSessionDto>(ResultType.Success, new LoadTestSessionDto(token));
    }
}
