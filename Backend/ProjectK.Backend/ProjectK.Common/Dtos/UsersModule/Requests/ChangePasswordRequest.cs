namespace ProjectK.Common.Dtos.UsersModule.Requests;

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
