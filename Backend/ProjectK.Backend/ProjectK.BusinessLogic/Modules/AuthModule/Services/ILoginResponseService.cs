using ProjectK.Common.Dtos.AuthModule;
using ProjectK.Common.Entities.AuthModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

public interface ILoginResponseService
{
    Task<LoginUserDto> CreateAsync(AppUser user, CancellationToken cancellationToken);
}
