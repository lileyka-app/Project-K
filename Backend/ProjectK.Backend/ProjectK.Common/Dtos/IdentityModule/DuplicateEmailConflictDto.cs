namespace ProjectK.Common.Dtos.IdentityModule;

/// <summary>One address that several members or accounts share.</summary>
public sealed record DuplicateEmailConflictDto(string Email, IReadOnlyList<Guid> MemberKeys, IReadOnlyList<Guid> UserKeys);
