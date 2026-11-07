using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Interfaces.Modules.KurinModule;

/// <summary>
/// The kurin's past: every office holding, the memberships that ended and the гуртки dissolved.
/// It is the one reader that sees past the dissolved-гурток filter. The <c>Find…</c> methods load
/// tracked, for a correction or a deletion; the <c>Get…</c> ones read.
/// </summary>
public interface IKurinArchiveRepository
{
    Task<IReadOnlyList<ArchivedOffice>> GetOfficesAsync(Guid kurinKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchivedMembership>> GetFormerMembershipsAsync(Guid kurinKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DissolvedGroup>> GetDissolvedGroupsAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>The holding with its office and the office's гурток, dissolved or not.</summary>
    Task<LeadershipHistory?> FindOfficeAsync(Guid historyKey, CancellationToken cancellationToken = default);

    Task<Membership?> FindMembershipAsync(Guid membershipKey, CancellationToken cancellationToken = default);

    /// <summary>Every гурток the membership was ever in, its current one included.</summary>
    Task<IReadOnlyList<Guid>> GetStintGroupKeysAsync(Guid membershipKey, CancellationToken cancellationToken = default);

    Task<Group?> FindDissolvedGroupAsync(Guid groupKey, CancellationToken cancellationToken = default);

    void RemoveOffice(LeadershipHistory history);

    /// <summary>
    /// Marks for deletion the person's holdings in this kurin that overlap the period and have ended,
    /// and reports them. A membership deleted «as if they had never been here» takes its offices with
    /// it; one still held is ended on the провід first (<see cref="HoldsOpenOfficeSinceAsync"/>).
    /// </summary>
    Task<IReadOnlyList<LeadershipHistory>> RemoveOfficesHeldAsync(
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

    /// <summary>Marks the membership for deletion; its гурток stints go with it in the database.</summary>
    /// <summary>
    /// Moves the edges of a membership's гурток stints with a corrected membership: the stint that
    /// opened at <paramref name="joinedWas"/> and the one that closed at <paramref name="leftWas"/>.
    /// </summary>
    Task MoveStintEdgesAsync(
        Guid membershipKey,
        DateTime joinedWas,
        DateTime joinedNow,
        DateTime leftWas,
        DateTime leftNow,
        CancellationToken cancellationToken = default);

    void RemoveMembership(Membership membership);

    /// <summary>
    /// Marks a dissolved гурток for deletion with its провід, that провід's history and the stints
    /// spent in it. Reports how many holdings went.
    /// </summary>
    Task<int> RemoveDissolvedGroupAsync(Group group, CancellationToken cancellationToken = default);
}
