using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Dtos.IdentityModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.LoadTest;

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
        if (string.IsNullOrEmpty(expectedKey) || !SecretComparisonRules.Matches(request.ApiKey, expectedKey))
        {
            return ServiceResult<LoadTestSessionDto>.Failure(
                ResultType.Unauthorized, IdentityErrors.InvalidApiKey, "Invalid or disabled load test API key.");
        }

        var user = await _userManager.FindByEmailAsync(LoadTestAccountEmail);
        if (user is null)
        {
            return ServiceResult<LoadTestSessionDto>.Failure(ResultType.NotFound, IdentityErrors.UserNotFound, "Load test user not found.");
        }

        var context = await _access.ResolveAsync(user, cancellationToken);
        var token = _jwtService.GenerateAccessToken(
            user.Id.ToString(), user.Email!, context.Roles, context.KurinKey?.ToString(), user.TwoFactorEnabled);

        return new ServiceResult<LoadTestSessionDto>(ResultType.Success, new LoadTestSessionDto(token));
    }
}
