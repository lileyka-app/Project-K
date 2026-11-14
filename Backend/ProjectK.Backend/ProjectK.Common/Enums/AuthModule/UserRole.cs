namespace ProjectK.Common.Enums.AuthModule;

// System-level roles only. Kurin authority is no longer modelled here — it comes from
// діловодські offices (see ProjectK.Common.Rules.AuthModule.SystemRole / RolePermissionMap).
public enum UserRole
{
    Admin,
    Member
}
