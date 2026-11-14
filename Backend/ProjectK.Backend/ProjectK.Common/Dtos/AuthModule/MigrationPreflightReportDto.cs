namespace ProjectK.Common.Dtos.AuthModule;

/// <summary>What the admin checks before a data migration: accounts and members that do not line up.</summary>
public sealed record MigrationPreflightReportDto(
    IReadOnlyList<DuplicateEmailConflictDto> DuplicateEmailConflicts,
    IReadOnlyList<OrphanMemberDto> OrphanMembers,
    IReadOnlyList<OrphanUserDto> OrphanUsers,
    IReadOnlyList<InconsistentLinkDto> InconsistentLinks,
    int TotalMembers,
    int TotalUsers);
