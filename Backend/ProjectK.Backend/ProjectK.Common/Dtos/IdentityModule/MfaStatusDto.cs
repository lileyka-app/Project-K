namespace ProjectK.Common.Dtos.IdentityModule;

/// <summary>Whether the signed-in account has a second factor, and whether its offices require one.</summary>
public sealed record MfaStatusDto(bool IsMfaEnabled, bool IsMfaRequired);
