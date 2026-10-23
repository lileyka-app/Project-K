using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;

public sealed class ProbeProgressResponse
{
    public Guid? ProbeProgressKey { get; init; }
    public Guid MemberKey { get; init; }
    public Guid KurinKey { get; init; }
    public string ProbeId { get; init; } = string.Empty;
    public ProbeProgressStatus Status { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public Guid? CompletedByUserKey { get; init; }
    public string? CompletedByName { get; init; }
    public string? CompletedByRole { get; init; }
    public DateTime? VerifiedAtUtc { get; init; }
    public Guid? VerifiedByUserKey { get; init; }
    public string? VerifiedByName { get; init; }
    public string? VerifiedByRole { get; init; }
    public IReadOnlyCollection<ProbeProgressAuditEventResponse> AuditTrail { get; init; } = [];
    public IReadOnlyCollection<ProbePointProgressResponse> PointSignatures { get; init; } = [];

    public static ProbeProgressResponse CreateNotStarted(
        Guid memberKey,
        Guid kurinKey,
        string probeId,
        IReadOnlyCollection<ProbePointProgressResponse>? pointSignatures = null)
    {
        return new ProbeProgressResponse
        {
            ProbeProgressKey = null,
            MemberKey = memberKey,
            KurinKey = kurinKey,
            ProbeId = probeId,
            Status = ProbeProgressStatus.NotStarted,
            AuditTrail = [],
            PointSignatures = pointSignatures ?? []
        };
    }

    /// <summary>The проба as the clients read it, its status history taken from the journal.</summary>
    /// <param name="entity">The проба.</param>
    /// <param name="journal">Its entries in the kurin's journal; entries about other records are skipped.</param>
    /// <param name="pointSignatures">Its points.</param>
    public static ProbeProgressResponse FromEntity(
        ProbeProgress entity,
        IEnumerable<KurinAuditEvent> journal,
        IReadOnlyCollection<ProbePointProgressResponse>? pointSignatures = null)
    {
        return new ProbeProgressResponse
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
}
