using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.BranchRequest;

internal static class KurinBranchRequesters
{
    public static async Task<string?> NameOfAsync(UserManager<AppUser> users, KurinBranchRequest request)
        => await users.FindByIdAsync(request.RequestedByUserKey.ToString()) is { } user
            ? $"{user.FirstName} {user.LastName}".Trim()
            : null;
}
