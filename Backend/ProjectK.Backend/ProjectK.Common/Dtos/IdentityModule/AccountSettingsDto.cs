namespace ProjectK.Common.Dtos.IdentityModule;

public record AccountSettingsDto(
    Guid UserKey,
    Guid? MemberKey,
    string Email,
    string? PhoneNumber,
    string FirstName,
    string LastName,
    string Role,
    bool TwoFactorEnabled,
    string? PendingEmail = null);
