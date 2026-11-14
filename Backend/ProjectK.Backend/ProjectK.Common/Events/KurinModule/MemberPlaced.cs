using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.KurinModule;

/// <summary>
/// A person's record says where they are placed — this kurin, this гурток. The kurin answers by
/// making its own membership say the same thing, because that row, and not the person's record, is
/// what every list and every access decision reads.
/// <para>
/// It exists only while <c>Member.KurinKey</c> does. Once placement is written by joining and
/// leaving a kurin outright, there is nothing left to mirror and this event goes with the column.
/// </para>
/// </summary>
public sealed record MemberPlaced(
    Guid MemberKey,
    Guid? UserKey,
    Guid KurinKey,
    Guid? GroupKey) : IDomainEvent;
