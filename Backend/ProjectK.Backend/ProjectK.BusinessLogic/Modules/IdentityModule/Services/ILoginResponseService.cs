using ProjectK.Common.Dtos.IdentityModule;
using ProjectK.Common.Entities.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Services;

public interface ILoginResponseService
{
    Task<LoginUserDto> CreateAsync(AppUser user, CancellationToken cancellationToken);
}
