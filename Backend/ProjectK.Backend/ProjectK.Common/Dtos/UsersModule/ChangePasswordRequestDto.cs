namespace ProjectK.Common.Dtos.UsersModule;

public record ChangePasswordRequestDto(string CurrentPassword, string NewPassword);
