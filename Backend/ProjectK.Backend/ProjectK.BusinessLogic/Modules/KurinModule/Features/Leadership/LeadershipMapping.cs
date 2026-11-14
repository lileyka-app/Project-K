using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using LeadershipEntity = ProjectK.Common.Entities.KurinModule.Leadership;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership;

/// <summary>A провід and the people who held its roles, as the client reads them.</summary>
public static class LeadershipMapping
{
    /// <summary>The провід with every holder it has had; the entity it leads is its kurin or its гурток.</summary>
    public static LeadershipDto ToDto(this LeadershipEntity leadership, IBlobReadLinks links) => new(
        LeadershipKey: leadership.LeadershipKey,
        Type: leadership.Type,
        EntityKey: leadership.Type == LeadershipType.Group
            ? leadership.GroupKey ?? Guid.Empty
            : leadership.KurinKey ?? Guid.Empty,
        KurinKey: leadership.KurinKey,
        GroupKey: leadership.GroupKey,
        LeadershipHistories: leadership.LeadershipHistories.Select(history => history.ToHolderDto(links)).ToList(),
        StartDate: leadership.StartDate,
        EndDate: leadership.EndDate);

    /// <summary>
    /// One holder of a role. The провід's key is not repeated on each holder; the person is filled
    /// only when the read loaded them.
    /// </summary>
    public static LeadershipHistoryMemberDto ToHolderDto(this LeadershipHistory history, IBlobReadLinks links) => new()
    {
        LeadershipHistoryKey = history.LeadershipHistoryKey,
        Role = history.Role.ToString(),
        StartDate = history.StartDate,
        EndDate = history.EndDate,
        Member = history.Member?.ToLookupDto(links)!
    };
}
