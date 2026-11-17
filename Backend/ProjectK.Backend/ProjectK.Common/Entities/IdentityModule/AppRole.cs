using Microsoft.AspNetCore.Identity;

namespace ProjectK.Common.Entities.IdentityModule;

public class AppRole : IdentityRole<Guid>
{
    public AppRole() : base() { }
    public AppRole(string roleName) : base(roleName)
    {
    }
}
