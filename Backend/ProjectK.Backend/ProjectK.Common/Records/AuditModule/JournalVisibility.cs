namespace ProjectK.Common.Records.AuditModule;

/// <summary>
/// An own-group reader's part of the journal: entries about people in <see cref="GroupKeys"/> at the
/// time, and entries about the reader. Entries about the kurin as a whole are not in it.
/// </summary>
/// <param name="GroupKeys">The гуртки the reader leads.</param>
/// <param name="OwnMemberKey">The reader's own person, when the account has one.</param>
public sealed record JournalVisibility(IReadOnlyCollection<Guid> GroupKeys, Guid? OwnMemberKey);
