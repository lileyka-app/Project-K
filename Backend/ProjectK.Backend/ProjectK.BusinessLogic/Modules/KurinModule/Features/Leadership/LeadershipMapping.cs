using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Rules.IdentityModule;
using LeadershipEntity = ProjectK.Common.Entities.KurinModule.Leadership;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership;

/// <summary>A провід and the people who held its roles, as the client reads them.</summary>
public static class LeadershipMapping
{
    /// <summary>
    /// The провід with every holder it has had; the entity it leads is its kurin or its гурток. The
    /// people come from the member module (<c>IMemberDirectory.GetLookupsAsync</c>), keyed by member.
    /// </summary>
    public static LeadershipDto ToDto(this LeadershipEntity leadership, IReadOnlyDictionary<Guid, MemberLookupDto> people) => new(
        LeadershipKey: leadership.LeadershipKey,
        Type: leadership.Type,
        EntityKey: leadership.Type == LeadershipType.Group
            ? leadership.GroupKey ?? Guid.Empty
            : leadership.KurinKey ?? Guid.Empty,
        KurinKey: leadership.KurinKey,
        GroupKey: leadership.GroupKey,
        LeadershipHistories: leadership.LeadershipHistories.Select(history => history.ToHolderDto(people)).ToList(),
        StartDate: leadership.StartDate,
        EndDate: leadership.EndDate);

    /// <summary>
    /// One holder of a role. The провід's key is not repeated on each holder; the person is filled
    /// only when the caller looked them up.
    /// </summary>
    public static LeadershipHistoryMemberDto ToHolderDto(this LeadershipHistory history, IReadOnlyDictionary<Guid, MemberLookupDto> people) => new()
    {
        LeadershipHistoryKey = history.LeadershipHistoryKey,
        Role = history.Role.ToString(),
        StartDate = history.StartDate,
        EndDate = history.EndDate,
        Member = people.GetValueOrDefault(history.MemberKey)!
    };

    /// <summary>Everyone who has held a role of the провід, to look up in one call.</summary>
    public static IReadOnlyCollection<Guid> HolderKeys(this LeadershipEntity leadership) =>
        leadership.LeadershipHistories.Select(history => history.MemberKey).Distinct().ToArray();

    /// <summary>
    /// A person the member module handed back as a summary. The summary carries no middle name and no
    /// verification, so the picker shows neither.
    /// </summary>
    public static MemberLookupDto ToLookupDto(this MemberSummary member, IBlobReadLinks links) => new()
    {
        MemberKey = member.MemberKey,
        UserKey = member.UserKey,
        FirstName = member.FirstName,
        LastName = member.LastName,
        ProfilePhotoBlobName = member.ProfilePhotoBlobName,
        ProfilePhotoUrl = links.For(member.ProfilePhotoBlobName)
    };

    /// <summary>
    /// A person in a picker, named by the office the read found them in. The photo stays a blob name
    /// until the caller signs it (<c>MemberLookupPhotoRules</c>); verification is not read.
    /// </summary>
    public static MemberLookupDto ToLookupDto(this MemberLookupRow row) => new()
    {
        MemberKey = row.MemberKey,
        UserKey = row.UserKey,
        FirstName = row.FirstName,
        MiddleName = row.MiddleName,
        LastName = row.LastName,
        ProfilePhotoBlobName = row.ProfilePhotoBlobName,
        UserRole = row.Office is null ? null : SystemRoleNames.ForOffice(row.Office.Type, row.Office.Role)
    };
}
