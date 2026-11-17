namespace ProjectK.Common.Dtos.IdentityModule;

/// <summary>An account no member is linked to.</summary>
public sealed record OrphanUserDto(Guid UserKey, string UserName, string Email);
