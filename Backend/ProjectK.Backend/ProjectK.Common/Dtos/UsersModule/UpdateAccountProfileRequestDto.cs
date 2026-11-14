namespace ProjectK.Common.Dtos.UsersModule;

public record UpdateAccountProfileRequestDto(string Email, string? PhoneNumber, string? CurrentPassword = null);
