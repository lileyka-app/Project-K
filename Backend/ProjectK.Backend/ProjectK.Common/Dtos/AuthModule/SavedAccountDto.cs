namespace ProjectK.Common.Dtos.AuthModule;

/// <summary>
/// An account this browser is also signed in to, as the account switcher lists it: enough to tell
/// the accounts apart and to colour the role the same way the sidebar does.
/// </summary>
public class SavedAccountDto
{
    public Guid UserKey { get; set; }
    public Guid? MemberKey { get; set; }
    public string Email { get; set; } = string.Empty;

    /// <summary>Empty for an account that has no member yet, such as a bare administrator.</summary>
    public string FullName { get; set; } = string.Empty;

    public string? ProfilePhotoUrl { get; set; }
    public bool IsAdmin { get; set; }

    /// <summary>System-role names for the kurin the account was last in, for display only.</summary>
    public IReadOnlyCollection<string> Roles { get; set; } = [];
}
