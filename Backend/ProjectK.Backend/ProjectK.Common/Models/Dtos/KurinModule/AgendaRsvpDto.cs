using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.KurinModule;

/// <summary>One person's answer, with display name and — for «Going» — whether capacity pushed them to the waitlist.</summary>
public sealed record AgendaRsvpDto
{
    public required Guid UserKey { get; init; }
    public required string DisplayName { get; init; }
    public required AgendaRsvpStatus Status { get; init; }
    public required DateTime RespondedAtUtc { get; init; }

    /// <summary>True for a «Going» answer beyond the category capacity; always false for NotGoing/Maybe.</summary>
    public required bool IsWaitlisted { get; init; }
}
