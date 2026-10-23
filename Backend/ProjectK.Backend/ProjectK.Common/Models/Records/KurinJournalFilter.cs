using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Records;

/// <summary>
/// Which of a kurin's journal entries to read. Every filter that is set narrows the result; the
/// period runs from <see cref="FromUtc"/> inclusive to <see cref="ToUtc"/> exclusive.
/// </summary>
/// <param name="KurinKey">The kurin.</param>
/// <param name="Area">Only this part of the kurin.</param>
/// <param name="MemberKey">Only entries about this person.</param>
/// <param name="ActorUserKey">Only what this account did.</param>
/// <param name="FromUtc">Not before.</param>
/// <param name="ToUtc">Before.</param>
/// <param name="Visibility">How far the reader sees; null for the whole kurin.</param>
public sealed record KurinJournalFilter(
    Guid KurinKey,
    AuditArea? Area = null,
    Guid? MemberKey = null,
    Guid? ActorUserKey = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    JournalVisibility? Visibility = null);

/// <summary>
/// An own-group reader's part of the journal: entries about people in <see cref="GroupKeys"/> at the
/// time, and entries about the reader. Entries about the kurin as a whole are not in it.
/// </summary>
/// <param name="GroupKeys">The гуртки the reader leads.</param>
/// <param name="OwnMemberKey">The reader's own person, when the account has one.</param>
public sealed record JournalVisibility(IReadOnlyCollection<Guid> GroupKeys, Guid? OwnMemberKey);
