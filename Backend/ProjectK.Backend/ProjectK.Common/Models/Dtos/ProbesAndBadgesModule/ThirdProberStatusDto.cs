using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;

/// <summary>
/// One third-prober as the referent's tool in Lilejka will read them (`GET api/kurin/{kurinKey}/third-probers`):
/// who, which probe, the stages, and how far the requirements are. Fields are added, never renamed,
/// so that tool can be built on top of this list.
/// </summary>
public sealed record ThirdProberStatusDto(
    Guid MemberKey,
    string FirstName,
    string LastName,
    string ProbeId,
    ThirdProbeStagesDto Stages,
    int MainProjects,
    int CompletedProjects,
    int BadgesDuringProbe,
    bool HasCamp,
    bool HasResume,
    bool ReflectionConfirmed);
