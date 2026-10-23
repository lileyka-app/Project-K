using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;

public sealed class BadgeProgressResponse
{
    public Guid BadgeProgressKey { get; init; }
    public Guid MemberKey { get; init; }
    public Guid KurinKey { get; init; }
    public string BadgeId { get; init; } = string.Empty;
    public BadgeProgressStatus Status { get; init; }
    public DateTime? SubmittedAtUtc { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
    public Guid? ReviewedByUserKey { get; init; }
    public string? ReviewedByName { get; init; }
    public string? ReviewedByRole { get; init; }
    public string? ReviewNote { get; init; }
    public IReadOnlyCollection<BadgeProgressAuditEventResponse> AuditTrail { get; init; } = [];

    public string? MemberFirstName { get; init; }
    public string? MemberLastName { get; init; }
    public string? MemberPhotoUrl { get; init; }

    /// <summary>The вмілість as the clients read it, its status history taken from the journal.</summary>
    /// <param name="entity">The вмілість.</param>
    /// <param name="journal">Its entries in the kurin's journal; entries about other records are skipped.</param>
    /// <param name="member">The person, for the review queue.</param>
    /// <param name="memberPhotoUrl">Their photo, for the review queue.</param>
    public static BadgeProgressResponse FromEntity(
        BadgeProgress entity,
        IEnumerable<KurinAuditEvent> journal,
        MemberSummary? member = null,
        string? memberPhotoUrl = null)
    {
        return new BadgeProgressResponse
        {
            BadgeProgressKey = entity.BadgeProgressKey,
            MemberKey = entity.MemberKey,
            KurinKey = entity.KurinKey,
            BadgeId = entity.BadgeId,
            Status = entity.Status,
            SubmittedAtUtc = entity.SubmittedAtUtc,
            ReviewedAtUtc = entity.ReviewedAtUtc,
            ReviewedByUserKey = entity.ReviewedByUserKey,
            ReviewedByName = entity.ReviewedByName,
            ReviewedByRole = entity.ReviewedByRole,
            ReviewNote = entity.ReviewNote,
            AuditTrail = ProgressTrail.ForBadge(entity.BadgeProgressKey, journal),
            MemberFirstName = member?.FirstName,
            MemberLastName = member?.LastName,
            MemberPhotoUrl = memberPhotoUrl
        };
    }
}
