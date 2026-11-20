namespace ProjectK.Common.Dtos.IdentityModule.Requests;

public record UpdateAccountProfileRequest(string Email, string? PhoneNumber, string? CurrentPassword = null);
