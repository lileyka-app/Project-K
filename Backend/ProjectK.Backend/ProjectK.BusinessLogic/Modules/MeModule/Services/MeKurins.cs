using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Dtos.MeModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Modules.MeModule.Services;

public static class MeKurins
{
    public static MyKurinRefDto Ref(MembershipRecord membership, Guid? currentKurinKey) => new()
    {
        KurinKey = membership.KurinKey,
        KurinNumber = membership.KurinNumber,
        Branches = [.. KurinBranchSet.Ordered(membership.Numbering, n => n.Branch)],
        NamedAfter = membership.KurinNamedAfter,
        IsCurrent = membership.KurinKey == currentKurinKey
    };
}
