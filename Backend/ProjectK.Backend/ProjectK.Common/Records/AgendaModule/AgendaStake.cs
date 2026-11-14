using ProjectK.Common.Entities.AgendaModule;

namespace ProjectK.Common.Records.AgendaModule;

/// <summary>The part of a task that is the viewer's own: a whole target, or their row in one done «кожному окремо».</summary>
public sealed record AgendaStake(AgendaAssignment Assignment, Guid? MemberKey);
