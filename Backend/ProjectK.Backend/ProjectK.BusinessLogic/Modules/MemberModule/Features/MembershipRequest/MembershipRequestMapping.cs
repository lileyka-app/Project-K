using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Rules.KurinModule;
using KurinEntity = ProjectK.Common.Entities.KurinModule.Kurin;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.MembershipRequest;

/// <summary>How a request between kurins names the kurins on either side.</summary>
public static class MembershipRequestMapping
{
    /// <summary>The kurin as read, or one nobody can find any more: a key and no name.</summary>
    public static MembershipRequestKurinDto ToRequestKurinDto(this KurinEntity? kurin, Guid kurinKey)
        => new(kurinKey, kurin?.Number, kurin?.NamedAfter, kurin is null ? [] : KurinBranchNames.NumberingOf(kurin));
}
