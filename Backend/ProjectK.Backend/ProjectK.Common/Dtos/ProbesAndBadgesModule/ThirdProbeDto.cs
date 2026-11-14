namespace ProjectK.Common.Dtos.ProbesAndBadgesModule;

/// <summary>Everything the «Третя проба» tab shows.</summary>
/// <param name="IsProjectProbe">Whether the probe stands at the third place of the person's line.
/// Only that one has a file and projects; for any other the rest is empty.</param>
/// <param name="CanEdit">The person themself, or whoever the kurin lets sign their probe.</param>
/// <param name="CanSign">May confirm the reflection, a project's start and its completion, and set the stages.</param>
/// <param name="CanUnsign">May take such a confirmation back.</param>
public sealed record ThirdProbeDto(
    bool IsProjectProbe,
    bool CanEdit,
    bool CanSign,
    bool CanUnsign,
    ThirdProbeFileDto? File,
    IReadOnlyList<ProbeProjectDto> Projects,
    IReadOnlyList<ThirdProbeBadgeDto> Badges,
    ProbeProjectLimitsDto Limits,
    ThirdProbeMentorOptionsDto? MentorOptions = null);
