namespace ProjectK.Common.Dtos.IdentityModule.Requests;

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
