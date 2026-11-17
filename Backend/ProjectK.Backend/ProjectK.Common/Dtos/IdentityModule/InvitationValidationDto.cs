namespace ProjectK.Common.Dtos.IdentityModule;

/// <summary>Who an invitation is for, shown on the activation page before a password is chosen.</summary>
public sealed record InvitationValidationDto(string Email, string FirstName, string LastName, bool IsValid);
