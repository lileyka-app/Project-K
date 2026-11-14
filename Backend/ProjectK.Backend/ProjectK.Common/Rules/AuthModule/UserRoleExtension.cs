using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.Common.Rules.AuthModule;

public static class UserRoleExtension
{
    public static string ToClaimValue(this UserRole role) => role.ToString();
}
