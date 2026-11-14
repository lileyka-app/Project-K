using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.MemberModule;

/// <summary>
/// These memberships are gone — the people behind them were removed. Raised by the kurin module
/// once it has dropped them, for whoever keys money or standing by membership rather than by person:
/// the вкладка keeps charges and payments per membership, and a row for a membership that no longer
/// exists is a nameless line in a гурток's table.
/// </summary>
public sealed record MembershipsRemoved(IReadOnlyCollection<Guid> MembershipKeys) : IDomainEvent;
