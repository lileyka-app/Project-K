namespace ProjectK.Common.Dtos.AuthModule;

public record MfaSetupResponseDto(string SharedKey, string AuthenticatorUri, string QrCodeBase64);
