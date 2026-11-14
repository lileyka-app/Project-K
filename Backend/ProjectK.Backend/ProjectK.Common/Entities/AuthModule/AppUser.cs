using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.Common.Entities.AuthModule;

public class AppUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;

    /// <summary>
    /// The kurin the account has stepped into, when it has said. Where it belongs is a question
    /// for membership; this is only a remembered choice, kept on the row so it survives a token
    /// refresh. Null means it has never chosen — an admin is then unscoped, system-wide, and
    /// anyone else is put where they stand (<c>AccessContextResolver</c>).
    /// </summary>
    public Guid? ActiveKurinKey { get; set; }
    public OnboardingStatus OnboardingStatus { get; set; }
}
