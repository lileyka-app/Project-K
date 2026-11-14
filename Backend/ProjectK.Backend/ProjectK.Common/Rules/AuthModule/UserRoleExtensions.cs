using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.Common.Rules.AuthModule;

public static class UserRoleExtensions
{
    public static string ToClaimValue(this UserRole role) => role.ToString();
}
