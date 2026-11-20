namespace ProjectK.Common.Dtos.AgendaModule;

/// <summary>An event group as the picker and management page see it.</summary>
public sealed record AgendaCategoryDto
{
    public required Guid AgendaCategoryKey { get; init; }
    public required Guid KurinKey { get; init; }
    public required string Name { get; init; }
    public required string ColorHex { get; init; }
    public required string? Icon { get; init; }
    public required int? Capacity { get; init; }
    public required bool WaitlistEnabled { get; init; }
    public required string? DefaultDescription { get; init; }
    public required bool RsvpRequired { get; init; }
    public required int? DefaultDurationMinutes { get; init; }
    public required int? ReminderLeadMinutes { get; init; }
    public required bool IsArchived { get; init; }
    public required bool IsKurinSchedule { get; init; }
}
