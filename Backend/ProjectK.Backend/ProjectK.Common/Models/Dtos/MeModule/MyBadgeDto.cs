using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.MeModule;

public sealed record MyBadgeDto
{
    public required string BadgeId { get; init; }
    public required string Title { get; init; }
    public required string? ImagePath { get; init; }
    public required BadgeProgressStatus Status { get; init; }
    public required DateTime? SubmittedAtUtc { get; init; }
    public required DateTime? ReviewedAtUtc { get; init; }
}
