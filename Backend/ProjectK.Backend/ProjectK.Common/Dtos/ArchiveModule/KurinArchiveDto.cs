namespace ProjectK.Common.Dtos.ArchiveModule;

/// <summary>The kurin's archive, as far as the viewer reaches it.</summary>
/// <param name="Offices">Every holding of an office, current ones too, newest first.</param>
/// <param name="Memberships">The memberships that ended.</param>
/// <param name="DissolvedGroups"></param>
/// <param name="CanDelete">Whether the viewer may turn the danger zone on.</param>
public sealed record KurinArchiveDto(
    IReadOnlyList<ArchivedOfficeDto> Offices,
    IReadOnlyList<ArchivedMembershipDto> Memberships,
    IReadOnlyList<DissolvedGroupDto> DissolvedGroups,
    bool CanDelete);
