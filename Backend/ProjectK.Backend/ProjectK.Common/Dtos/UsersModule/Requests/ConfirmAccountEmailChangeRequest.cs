namespace ProjectK.Common.Dtos.UsersModule.Requests;

public record ConfirmAccountEmailChangeRequest(string Email, string Token);
