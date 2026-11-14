using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Badge;

internal static class BadgeMapping
{
    /// <summary>The вмілість as the clients read it, its status history taken from the journal.</summary>
    /// <param name="entity">The вмілість.</param>
    /// <param name="journal">Its entries in the kurin's journal; entries about other records are skipped.</param>
    /// <param name="member">The person, for the review queue.</param>
    /// <param name="memberPhotoUrl">Their photo, for the review queue.</param>
    public static BadgeProgressDto ToDto(
        this BadgeProgress entity,
        IEnumerable<KurinAuditEvent> journal,
        MemberSummary? member = null,
        string? memberPhotoUrl = null) => new()
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
