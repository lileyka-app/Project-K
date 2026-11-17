using ProjectK.Common.Enums.IdentityModule;

namespace ProjectK.Common.Rules.IdentityModule;

public static class UserRoleExtensions
{
    public static string ToClaimValue(this UserRole role) => role.ToString();
}
