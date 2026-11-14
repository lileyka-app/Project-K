using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;

/// <summary>A проба as a person walks it: its status, who completed and verified it, its history and its points.</summary>
/// <remarks><see cref="ProbeProgressKey"/> is null for a проба not started yet.</remarks>
public sealed record ProbeProgressDto
{
    public required Guid? ProbeProgressKey { get; init; }
    public required Guid MemberKey { get; init; }
    public required Guid KurinKey { get; init; }
    public required string ProbeId { get; init; }
    public required ProbeProgressStatus Status { get; init; }
    public required DateTime? CompletedAtUtc { get; init; }
    public required Guid? CompletedByUserKey { get; init; }
    public required string? CompletedByName { get; init; }
    public required string? CompletedByRole { get; init; }
    public required DateTime? VerifiedAtUtc { get; init; }
    public required Guid? VerifiedByUserKey { get; init; }
    public required string? VerifiedByName { get; init; }
    public required string? VerifiedByRole { get; init; }
    public required IReadOnlyCollection<ProbeProgressAuditEventDto> AuditTrail { get; init; }
    public required IReadOnlyCollection<ProbePointProgressDto> PointSignatures { get; init; }
}
