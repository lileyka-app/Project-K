using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Probe;

internal static class ProbeMapping
{
    /// <summary>A проба nobody has started: no record yet, only the points as they stand.</summary>
    public static ProbeProgressDto NotStarted(
        Guid memberKey,
        Guid kurinKey,
        string probeId,
        IReadOnlyCollection<ProbePointProgressDto>? pointSignatures = null) => new()
    {
        ProbeProgressKey = null,
        MemberKey = memberKey,
        KurinKey = kurinKey,
        ProbeId = probeId,
        Status = ProbeProgressStatus.NotStarted,
        CompletedAtUtc = null,
        CompletedByUserKey = null,
        CompletedByName = null,
        CompletedByRole = null,
        VerifiedAtUtc = null,
        VerifiedByUserKey = null,
        VerifiedByName = null,
        VerifiedByRole = null,
        AuditTrail = [],
        PointSignatures = pointSignatures ?? []
    };

    /// <summary>The проба as the clients read it, its status history taken from the journal.</summary>
    /// <param name="entity">The проба.</param>
    /// <param name="journal">Its entries in the kurin's journal; entries about other records are skipped.</param>
    /// <param name="pointSignatures">Its points.</param>
    public static ProbeProgressDto ToDto(
        this ProbeProgress entity,
        IEnumerable<KurinAuditEvent> journal,
        IReadOnlyCollection<ProbePointProgressDto>? pointSignatures = null) => new()
    {
        ProbeProgressKey = entity.ProbeProgressKey,
        MemberKey = entity.MemberKey,
        KurinKey = entity.KurinKey,
        ProbeId = entity.ProbeId,
        Status = entity.Status,
        CompletedAtUtc = entity.CompletedAtUtc,
        CompletedByUserKey = entity.CompletedByUserKey,
        CompletedByName = entity.CompletedByName,
        CompletedByRole = entity.CompletedByRole,
        VerifiedAtUtc = entity.VerifiedAtUtc,
        VerifiedByUserKey = entity.VerifiedByUserKey,
        VerifiedByName = entity.VerifiedByName,
        VerifiedByRole = entity.VerifiedByRole,
        AuditTrail = ProgressTrail.ForProbe(entity.ProbeProgressKey, journal),
        PointSignatures = pointSignatures ?? []
    };
}
