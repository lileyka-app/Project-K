namespace ProjectK.Common.Dtos.IdentityModule;

public record MfaRecoveryCodesResponseDto(IEnumerable<string> RecoveryCodes);
