using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;

/// <summary>A вмілість a person works on, with its status history; the member's name and photo only in the review queue.</summary>
public sealed record BadgeProgressDto
{
    public required Guid BadgeProgressKey { get; init; }
    public required Guid MemberKey { get; init; }
    public required Guid KurinKey { get; init; }
    public required string BadgeId { get; init; }
    public required BadgeProgressStatus Status { get; init; }
    public required DateTime? SubmittedAtUtc { get; init; }
    public required DateTime? ReviewedAtUtc { get; init; }
    public required Guid? ReviewedByUserKey { get; init; }
    public required string? ReviewedByName { get; init; }
    public required string? ReviewedByRole { get; init; }
    public required string? ReviewNote { get; init; }
    public required IReadOnlyCollection<BadgeProgressAuditEventDto> AuditTrail { get; init; }

    public required string? MemberFirstName { get; init; }
    public required string? MemberLastName { get; init; }
    public required string? MemberPhotoUrl { get; init; }
}
