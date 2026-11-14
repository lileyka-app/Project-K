using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Dtos.KurinModule;

public sealed record AgendaAssignmentDto
{
    public required Guid AgendaAssignmentKey { get; set; }
    public required AgendaTargetType TargetType { get; set; }
    public required Guid TargetKey { get; set; }

    /// <summary>Human label for the target (kurin number, group name, member full name).</summary>
    public required string? Label { get; set; }

    public required AgendaCompletionMode CompletionMode { get; set; }

    /// <summary>The target's state; in «кожному окремо» what its people's parts add up to now.</summary>
    public required AgendaItemStatus Status { get; set; }

    /// <summary>Who last moved it and when — never a «done» without a name.</summary>
    public required string? StatusChangedByName { get; set; }
    public required DateTime? StatusChangedAtUtc { get; set; }

    /// <summary>Whether this viewer may move the target's single state (never in «кожному окремо»).</summary>
    public required bool CanChangeStatus { get; set; }

    /// <summary>«Кожному окремо» only: how many of the people in the target are done, of how many.</summary>
    public required int? DoneCount { get; set; }
    public required int? PeopleCount { get; set; }

    /// <summary>
    /// «Кожному окремо», for those who run the target: everyone in it with their part. Others see the
    /// counts and their own part, not the list — a youth has no need of who else has not paid.
    /// </summary>
    public required List<AgendaPartDto>? Parts { get; set; }
}
