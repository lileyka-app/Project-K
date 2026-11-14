using Microsoft.AspNetCore.Identity;

namespace ProjectK.Common.Entities.AuthModule;

public class AppRole : IdentityRole<Guid>
{
    public AppRole() : base() { }
    public AppRole(string roleName) : base(roleName)
    {
    }
}
