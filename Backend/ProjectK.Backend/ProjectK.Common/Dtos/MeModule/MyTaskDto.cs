using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.Common.Dtos.MeModule;

/// <summary>A task the person is on the hook for that is not done.</summary>
public sealed record MyTaskDto
{
    public required Guid AgendaItemKey { get; init; }
    public required MyKurinRefDto Kurin { get; init; }
    public required string Title { get; init; }
    public required AgendaItemStatus Status { get; init; }
    public required DateTime? StartUtc { get; init; }
    public required DateTime? EndUtc { get; init; }
    /// <summary>Whether the status can be moved from here: the right to, in the kurin the token acts in.</summary>
    public required bool CanChangeStatus { get; init; }
}
