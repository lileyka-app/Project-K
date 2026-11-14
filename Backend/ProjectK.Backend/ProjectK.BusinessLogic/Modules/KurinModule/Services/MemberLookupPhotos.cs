using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Models.Dtos.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// Turns the blob names the lookup projections carry into signed links. Without it the КВ and the
/// mentor pickers showed initials for people whose photo the member list already showed.
/// </summary>
public static class MemberLookupPhotos
{
    public static TList WithPhotoLinks<TList>(this TList lookups, IBlobReadLinks links)
        where TList : IEnumerable<MemberLookupDto>
    {
        foreach (var lookup in lookups)
        {
            lookup.ProfilePhotoUrl = links.For(lookup.ProfilePhotoBlobName);
        }

        return lookups;
    }
}
