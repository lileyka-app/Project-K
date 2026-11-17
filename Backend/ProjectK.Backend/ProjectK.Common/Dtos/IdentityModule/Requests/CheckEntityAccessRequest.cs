namespace ProjectK.Common.Dtos.IdentityModule.Requests;

public class CheckEntityAccessRequest
{
    public string EntityType { get; set; }
    public string EntityKey { get; set; }
    public string? Action { get; set; }

    // Kept for backward compatibility with existing clients.
    // Backend ignores this value for security decisions and uses claims/context scope only.
    public string? ActiveKurinKey { get; set; }
}
