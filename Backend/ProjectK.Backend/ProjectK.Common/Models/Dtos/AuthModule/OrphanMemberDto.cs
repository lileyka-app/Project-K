namespace ProjectK.Common.Models.Dtos.AuthModule;

/// <summary>A member that points at an account which does not exist.</summary>
public sealed record OrphanMemberDto(Guid MemberKey, string Name, string Email, Guid? MissingUserKey);
