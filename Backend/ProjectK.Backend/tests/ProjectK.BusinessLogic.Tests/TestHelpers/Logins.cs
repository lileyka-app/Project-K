using ProjectK.Common.Dtos.IdentityModule;

namespace ProjectK.BusinessLogic.Tests.TestHelpers;

/// <summary>A sign-in with nothing in it, for tests that fill only the fields they look at (<c>Logins.Empty with { … }</c>).</summary>
public static class Logins
{
    public static LoginUserDto Empty { get; } = new()
    {
        UserKey = Guid.Empty,
        MemberKey = null,
        Email = string.Empty,
        IsAdmin = false,
        Permissions = [],
        Roles = [],
        KurinKey = null,
        RequiresMfa = false,
        Tokens = null
    };
}
