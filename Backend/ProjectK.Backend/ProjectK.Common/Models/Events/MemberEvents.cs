using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Events;

/// <summary>
/// A verified profile was edited, so what was checked no longer matches what is stored. It is news for
/// the провід who verify, not for the person: someone has to look at the data again.
/// </summary>
public sealed record MemberProfileWentStale(
    Guid MemberKey,
    string MemberName,
    Guid? ActorUserKey) : IDomainEvent;

/// <summary>A profile was checked against the person and confirmed current.</summary>
public sealed record MemberProfileVerified(
    Guid MemberKey,
    Guid MemberUserKey,
    Guid? ActorUserKey) : IDomainEvent;

/// <summary>An award was put forward and is waiting for the провід to look at it.</summary>
public sealed record MemberAwardSubmitted(
    Guid MemberAwardKey,
    Guid MemberKey,
    string MemberName,
    Guid KurinKey,
    Guid? GroupKey,
    Guid? ActorUserKey) : IDomainEvent;

/// <summary>An award was looked at and either stands or does not.</summary>
public sealed record MemberAwardReviewed(
    Guid MemberAwardKey,
    Guid MemberKey,
    Guid MemberUserKey,
    bool IsApproved,
    Guid? ActorUserKey) : IDomainEvent;

/// <summary>A пересторога was written against a member.</summary>
public sealed record MemberWarningAssigned(
    Guid MemberWarningKey,
    Guid MemberKey,
    Guid MemberUserKey,
    MemberWarningLevel Level,
    Guid? ActorUserKey) : IDomainEvent;

/// <summary>A вмілість was handed in and is waiting to be reviewed.</summary>
public sealed record BadgeProgressSubmitted(
    Guid BadgeProgressKey,
    string BadgeId,
    Guid MemberKey,
    string MemberName,
    Guid KurinKey,
    Guid? GroupKey,
    Guid? ActorUserKey) : IDomainEvent;

/// <summary>
/// A вмілість was reviewed. <paramref name="ConfirmationWithdrawn"/> separates "not accepted yet"
/// from "an earlier confirmation was taken back", which read the same to the member otherwise.
/// </summary>
public sealed record BadgeProgressReviewed(
    Guid BadgeProgressKey,
    string BadgeId,
    Guid MemberKey,
    Guid MemberUserKey,
    bool IsApproved,
    bool ConfirmationWithdrawn,
    Guid? ActorUserKey) : IDomainEvent;

/// <summary>
/// These people are gone from the system. Whoever keeps anything keyed by a member — progress,
/// history, assignments — clears it in response; the module that removed them does not need to know
/// who that is.
/// </summary>
public sealed record MembersRemoved(IReadOnlyCollection<Guid> MemberKeys) : IDomainEvent;

/// <summary>
/// These memberships are gone — the people behind them were removed. Raised by the kurin module
/// once it has dropped them, for whoever keys money or standing by membership rather than by person:
/// the вкладка keeps charges and payments per membership, and a row for a membership that no longer
/// exists is a nameless line in a гурток's table.
/// </summary>
public sealed record MembershipsRemoved(IReadOnlyCollection<Guid> MembershipKeys) : IDomainEvent;

/// <summary>
/// A person's record was tied to the account they sign in with. Anything that keeps a copy of that
/// link — a membership does, so authorization never has to read the person — updates itself here.
/// </summary>
public sealed record MemberAccountLinked(Guid MemberKey, Guid UserKey) : IDomainEvent;

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
