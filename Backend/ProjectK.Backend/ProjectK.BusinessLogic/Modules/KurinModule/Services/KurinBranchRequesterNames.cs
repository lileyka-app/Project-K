using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

internal static class KurinBranchRequesterNames
{
    public static async Task<string?> NameOfAsync(UserManager<AppUser> users, KurinBranchRequest request)
        => await users.FindByIdAsync(request.RequestedByUserKey.ToString()) is { } user
            ? $"{user.FirstName} {user.LastName}".Trim()
            : null;
}
