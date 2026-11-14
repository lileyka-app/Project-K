namespace ProjectK.Common.Dtos.AuthModule;

public record MfaRecoveryCodesResponseDto(IEnumerable<string> RecoveryCodes);
