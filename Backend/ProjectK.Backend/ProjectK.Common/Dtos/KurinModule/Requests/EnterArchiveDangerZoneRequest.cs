namespace ProjectK.Common.Dtos.KurinModule.Requests;

/// <summary>The account proving itself again: a password, or an authenticator or recovery code.</summary>
public sealed record EnterArchiveDangerZoneRequest(string? Password, string? Code);
