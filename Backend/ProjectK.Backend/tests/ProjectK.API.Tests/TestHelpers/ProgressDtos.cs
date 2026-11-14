using ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Tests.TestHelpers;

/// <summary>Probe and badge answers for a mocked mediator: only who and what, nobody signed yet.</summary>
public static class ProgressDtos
{
    public static BadgeProgressDto Badge(Guid memberKey, Guid kurinKey, string badgeId, BadgeProgressStatus status) => new()
    {
        BadgeProgressKey = Guid.NewGuid(),
        MemberKey = memberKey,
        KurinKey = kurinKey,
        BadgeId = badgeId,
        Status = status,
        SubmittedAtUtc = null,
        ReviewedAtUtc = null,
        ReviewedByUserKey = null,
        ReviewedByName = null,
        ReviewedByRole = null,
        ReviewNote = null,
        AuditTrail = [],
        MemberFirstName = null,
        MemberLastName = null,
        MemberPhotoUrl = null
    };

    public static ProbeProgressDto Probe(Guid memberKey, Guid kurinKey, string probeId, ProbeProgressStatus status = ProbeProgressStatus.NotStarted) => new()
    {
        ProbeProgressKey = status == ProbeProgressStatus.NotStarted ? null : Guid.NewGuid(),
        MemberKey = memberKey,
        KurinKey = kurinKey,
        ProbeId = probeId,
        Status = status,
        CompletedAtUtc = null,
        CompletedByUserKey = null,
        CompletedByName = null,
        CompletedByRole = null,
        VerifiedAtUtc = null,
        VerifiedByUserKey = null,
        VerifiedByName = null,
        VerifiedByRole = null,
        AuditTrail = [],
        PointSignatures = []
    };
}
