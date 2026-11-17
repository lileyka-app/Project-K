using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Records.KurinModule;

namespace ProjectK.Common.Contracts.KurinModule;

/// <summary>
/// The kurin's past: every office holding, the memberships that ended and the гуртки dissolved.
/// It is the one reader that sees past the dissolved-гурток filter. The rows it reads and changes
/// belong to the member and kurin modules, so it answers in records and is told what to change by
/// key; the archive never holds their entities. The <c>Find…</c> methods load the row tracked, for
/// the correction or deletion that follows; the <c>Get…</c> ones read.
/// </summary>
public interface IKurinArchiveRepository
{
    Task<IReadOnlyList<ArchivedOffice>> GetOfficesAsync(Guid kurinKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchivedMembership>> GetFormerMembershipsAsync(Guid kurinKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DissolvedGroup>> GetDissolvedGroupsAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>The kurin's own names for its roles; a role it did not rename is left out.</summary>
    Task<IReadOnlyDictionary<LeadershipRole, string>> GetRoleNamesAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>The holding with its office's гурток, dissolved or not; null unless it is this kurin's.</summary>
    Task<ArchivedOffice?> FindOfficeAsync(Guid kurinKey, Guid historyKey, CancellationToken cancellationToken = default);

    /// <summary>The membership, ended or not; null unless it is in this kurin.</summary>
    Task<MembershipPeriod?> FindMembershipAsync(Guid kurinKey, Guid membershipKey, CancellationToken cancellationToken = default);

    /// <summary>Every гурток the membership was ever in, its current one included.</summary>
    Task<IReadOnlyList<Guid>> GetStintGroupKeysAsync(Guid membershipKey, CancellationToken cancellationToken = default);

    /// <summary>The dissolved гурток; null unless it is this kurin's.</summary>
    Task<DissolvedGroup?> FindDissolvedGroupAsync(Guid kurinKey, Guid groupKey, CancellationToken cancellationToken = default);

    /// <summary>Gives a holding its corrected role and dates.</summary>
    Task CorrectOfficeAsync(Guid historyKey, LeadershipRole role, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

    Task RemoveOfficeAsync(Guid historyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks for deletion the person's holdings in this kurin that overlap the period and have ended,
    /// and reports how many. A membership deleted «as if they had never been here» takes its offices
    /// with it; one still held is ended on the провід first (<see cref="HoldsOpenOfficeSinceAsync"/>).
    /// </summary>
    Task<int> RemoveOfficesHeldAsync(
        Guid memberKey,
        Guid kurinKey,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the person still holds, open, an office in this kurin that began on or before
    /// <paramref name="before"/> — one seated during a membership and never ended.
    /// </summary>
    Task<bool> HoldsOpenOfficeSinceAsync(Guid memberKey, Guid kurinKey, DateOnly before, CancellationToken cancellationToken = default);

    /// <summary>The person's other memberships in this kurin, as when they joined and left (null: still in).</summary>
    Task<IReadOnlyList<(DateTime JoinedAtUtc, DateTime? LeftAtUtc)>> GetOtherPeriodsAsync(
        Guid memberKey,
        Guid kurinKey,
        Guid exceptMembershipKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives an ended membership its corrected kind and dates, and moves the edges of its гурток
    /// stints with them: the stint that opened when it began and the one that closed when it ended.
    /// </summary>
    Task CorrectMembershipAsync(
        Guid membershipKey,
        MembershipKind kind,
        DateTime joinedAtUtc,
        DateTime leftAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Marks the membership for deletion; its гурток stints go with it in the database.</summary>
    Task RemoveMembershipAsync(Guid membershipKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a dissolved гурток for deletion with its провід, that провід's history and the stints
    /// spent in it. Reports how many holdings went.
    /// </summary>
    Task<int> RemoveDissolvedGroupAsync(Guid groupKey, CancellationToken cancellationToken = default);
}
