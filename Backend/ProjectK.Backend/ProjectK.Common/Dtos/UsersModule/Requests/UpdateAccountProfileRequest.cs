namespace ProjectK.Common.Dtos.UsersModule.Requests;

public record UpdateAccountProfileRequest(string Email, string? PhoneNumber, string? CurrentPassword = null);
