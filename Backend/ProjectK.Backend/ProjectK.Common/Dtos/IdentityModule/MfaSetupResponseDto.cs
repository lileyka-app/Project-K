namespace ProjectK.Common.Dtos.IdentityModule;

public record MfaSetupResponseDto(string SharedKey, string AuthenticatorUri, string QrCodeBase64);
