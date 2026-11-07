using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Interfaces.Modules.KurinModule;

public interface ILeadershipRepository
{
    Task<Leadership?> GetByKeyAsync(Guid leadershipKey, CancellationToken cancellationToken = default);
    Task<IEnumerable<Leadership>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Leadership>> GetAllByTypeAsync(LeadershipType type, Guid entityKey, CancellationToken cancellationToken = default);

    void Add(Leadership leadership, CancellationToken cancellationToken = default);
    void Update(Leadership leadership, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the office record a гурток carries for deletion and reports its keys; the history
    /// cascades in the database.
    /// <para>
    /// Needed because <c>Leadership.Group</c> is <c>Restrict</c>: a гурток that still carries a
    /// провід cannot be deleted, and the database refuses it rather than cascading. The keys come
    /// back so the caller can clear what still points at those offices.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Guid>> DeleteForGroupAsync(Guid groupKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends the провід of a гурток being dissolved and everyone still seated in it, on
    /// <paramref name="on"/>. The records stay for the archive. Reports the office keys.
    /// </summary>
    Task<IReadOnlyList<Guid>> CloseForGroupAsync(Guid groupKey, DateOnly on, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks every office in a kurin for deletion — its own and its гуртки's — and reports the keys.
    /// <para>
    /// Both <c>Leadership.Kurin</c> and <c>Leadership.Group</c> are <c>NO ACTION</c>. Гуртки cascade
    /// with the kurin, but that cascade is itself refused while an office still points at one, so
    /// the гурток offices have to go too.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Guid>> DeleteForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    Task<IEnumerable<LeadershipHistory>> GetLeadershipHistoriesAsync(Guid leadershipKey, CancellationToken cancellationToken = default);
    void LeadershipHistoriesRemoveRange(IEnumerable<LeadershipHistory> histories);

    /// <summary>The offices a member currently holds (active <see cref="LeadershipHistory"/>), each with its провід.</summary>
    Task<IReadOnlyList<MemberOffice>> GetActiveOfficesForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The offices an account currently holds <b>inside one kurin</b> — the question access
    /// control actually asks. An office of another kurin does not appear, and neither does one
    /// held under a membership that has ended.
    /// <para>
    /// It starts from <c>Memberships</c> and never touches the member table: who someone is has
    /// no bearing on what they may do, and this read is the place that would quietly reintroduce
    /// the dependency.
    /// </para>
    /// A mentor assignment to a гурток of this kurin counts as a Виховник (КВ) office — the
    /// assignment is what makes someone one, and it is folded in here so there is one answer.
    /// </summary>
    Task<IReadOnlyList<MemberOffice>> GetActiveOfficesForAccountInKurinAsync(
        Guid userKey,
        Guid kurinKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Member keys holding an active office in one of <paramref name="roles"/>, optionally scoped to a
    /// kurin (курінний/КВ offices) or a specific group (гуртковий offices).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetActiveOfficeMemberKeysAsync(
        IReadOnlyCollection<LeadershipRole> roles,
        Guid? kurinKey = null,
        Guid? groupKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Members currently holding a kurin- or КВ-scoped office in the kurin, one lookup row per office,
    /// with <c>UserRole</c> set to the office role name. Used to render the КВ / провід tables.
    /// </summary>
    Task<IReadOnlyList<MemberLookupDto>> GetOfficeMembersLookupAsync(
        Guid kurinKey,
        LeadershipType type,
        CancellationToken cancellationToken = default);

    /// <summary>Every провід/КВ that belongs to the kurin (курінь/КВ by kurin, гуртковий by its group).</summary>
    Task<IReadOnlyList<LeadershipRef>> GetLeadershipRefsForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>User keys of the members who currently hold an office in the given провід (active history).</summary>
    Task<IReadOnlyList<Guid>> GetActiveMemberUserKeysForLeadershipAsync(Guid leadershipKey, CancellationToken cancellationToken = default);

    /// <summary>Member keys of those who currently hold an office in the given провід.</summary>
    Task<IReadOnlyList<Guid>> GetActiveMemberKeysForLeadershipAsync(Guid leadershipKey, CancellationToken cancellationToken = default);

    /// <summary>The провід keys a member currently belongs to (active <see cref="LeadershipHistory"/>).</summary>
    Task<IReadOnlyList<Guid>> GetActiveLeadershipKeysForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default);
}
