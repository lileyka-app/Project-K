namespace ProjectK.Common.Dtos.IdentityModule.Requests;

public record ConfirmAccountEmailChangeRequest(string Email, string Token);
