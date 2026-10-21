using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Interfaces.Modules.KurinModule;

public interface IMemberRepository : IBaseEntityRepository<Member>
{
    Task<IEnumerable<Member>> GetAllAsync(Guid groupKey, CancellationToken cancellationToken = default);
    Task<IEnumerable<Member>> GetAllByKurinKeyAsync(Guid kurinKey, CancellationToken cancellationToken = default);
    Task<IEnumerable<MemberListItemDto>> GetListItemsByKurinKeyAsync(Guid kurinKey, MemberFieldVisibility visibility, CancellationToken cancellationToken = default);
    Task<IEnumerable<MemberListItemDto>> GetListItemsByGroupKeyAsync(Guid groupKey, MemberFieldVisibility visibility, CancellationToken cancellationToken = default);
    Task<IEnumerable<MemberLookupDto>> GetMentorCandidatesLookupAsync(Guid kurinKey, CancellationToken cancellationToken = default);
    Task<Member?> GetByUserKeyAsync(Guid userKey, CancellationToken cancellationToken = default);
    // Narrow reads for handlers that only need one field — avoids loading the full
    // Include graph of GetByKeyAsync just to read a single key. Null means no such member.
    Task<Guid?> GetUserKeyByMemberAsync(Guid memberKey, CancellationToken cancellationToken = default);
    Task<Guid?> GetKurinKeyByMemberAsync(Guid memberKey, CancellationToken cancellationToken = default);

    // Summary projections for IMemberDirectory: other modules read people through the contract,
    // and none of them needs the entity graph these narrow reads replace.
    Task<MemberSummary?> GetSummaryByKeyAsync(Guid memberKey, CancellationToken cancellationToken = default);
    Task<MemberSummary?> GetSummaryByUserKeyAsync(Guid userKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<MemberSummary>> GetSummariesByKurinKeyAsync(Guid kurinKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<MemberSummary>> GetSummariesByKurinKeyAsync(Guid kurinKey, MemberSelection selection, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<MemberSummary>> GetAllSummariesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<MemberSummary>> GetSummariesByKeysAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>The person a public code names, with how many kurins they currently belong to.</summary>
    Task<MemberCard?> GetCardByPublicIdAsync(string publicId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the member exists and which account it is linked to. The account flow asks this
    /// before it writes anything, and would otherwise load the whole graph for two fields.
    /// Null means no such member.
    /// </summary>
    Task<MemberAccountLink?> GetAccountLinkAsync(Guid memberKey, CancellationToken cancellationToken = default);
    Task<Member?> GetTrackedByUserKeyAsync(Guid userKey, CancellationToken cancellationToken = default);
    Task<Member?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// People who might already be someone in an imported roster: anyone carrying one of these
    /// addresses or phone numbers, or one of these surnames. Deliberately wider than the answer —
    /// a surname is not a match on its own, and the caller narrows it by name and birthday. The
    /// point is to ask once instead of once per row.
    /// </summary>
    Task<IReadOnlyCollection<MemberIdentity>> FindPossibleMatchesAsync(
        IReadOnlyCollection<string> emails,
        IReadOnlyCollection<string> phoneNumbers,
        IReadOnlyCollection<string> lastNames,
        CancellationToken cancellationToken = default);

    #region PlastLevelHistory Methods
    #endregion

    #region LeadershipHistory Methods

    #endregion
}
