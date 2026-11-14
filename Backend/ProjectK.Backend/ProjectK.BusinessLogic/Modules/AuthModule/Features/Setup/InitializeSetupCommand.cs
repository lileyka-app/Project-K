using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.AuthModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuthModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Setup;

public record InitializeSetupCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    bool EnforcePrivilegedMfa = false,
    bool SeedDemoData = false
) : IRequest<ServiceResult<LoginUserDto>>;

public class InitializeSetupCommandHandler : IRequestHandler<InitializeSetupCommand, ServiceResult<LoginUserDto>>
{
    // Guards against two concurrent initialize calls both passing the "no admins yet" check.
    private static readonly SemaphoreSlim InitializationLock = new(1, 1);

    private readonly UserManager<AppUser> _userManager;
    private readonly ILoginResponseService _loginResponseService;
    private readonly ISystemSettingsService _systemSettings;
    private readonly IDemoDataSeeder _demoDataSeeder;

    public InitializeSetupCommandHandler(
        UserManager<AppUser> userManager,
        ILoginResponseService loginResponseService,
        ISystemSettingsService systemSettings,
        IDemoDataSeeder demoDataSeeder)
    {
        _userManager = userManager;
        _loginResponseService = loginResponseService;
        _systemSettings = systemSettings;
        _demoDataSeeder = demoDataSeeder;
    }

    public async Task<ServiceResult<LoginUserDto>> Handle(InitializeSetupCommand request, CancellationToken cancellationToken)
    {
        await InitializationLock.WaitAsync(cancellationToken);
        try
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync(SystemRole.Admin);
            if (adminUsers.Any())
            {
                return ServiceResult<LoginUserDto>.Failure(
                    ResultType.Forbidden,
                    AuthErrors.AlreadyInitialized,
                    "System is already initialized.");
            }

            var user = new AppUser
            {
                UserName = request.Email,
                Email = request.Email,
                EmailConfirmed = true,
                FirstName = request.FirstName,
                LastName = request.LastName,
                OnboardingStatus = OnboardingStatus.Active
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                return ServiceResult<LoginUserDto>.Failure(
                    ResultType.BadRequest,
                    AuthErrors.SetupFailed,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            await _userManager.AddToRoleAsync(user, SystemRole.Admin);

            await _systemSettings.SetValueAsync(
                SystemSettingKeys.EnforcePrivilegedMfa,
                request.EnforcePrivilegedMfa ? "true" : "false",
                cancellationToken);

            if (request.SeedDemoData)
            {
                await _demoDataSeeder.SeedAsync(cancellationToken);
            }

            var response = await _loginResponseService.CreateAsync(user, cancellationToken);
            return new ServiceResult<LoginUserDto>(ResultType.Success, response);
        }
        finally
        {
            InitializationLock.Release();
        }
    }
}
