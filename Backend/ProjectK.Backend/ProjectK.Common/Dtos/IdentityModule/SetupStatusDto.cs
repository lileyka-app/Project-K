namespace ProjectK.Common.Dtos.IdentityModule;

/// <summary>Whether the instance has its first administrator yet.</summary>
public sealed record SetupStatusDto(bool IsInitialized);
