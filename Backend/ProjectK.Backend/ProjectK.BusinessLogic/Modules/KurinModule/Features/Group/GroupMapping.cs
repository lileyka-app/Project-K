using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Rules.Shared;
using GroupEntity = ProjectK.Common.Entities.KurinModule.Group;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Group;

internal static class GroupMapping
{
    public static GroupDto ToDto(this GroupEntity group, IBlobReadLinks links) => new(
        GroupKey: group.GroupKey,
        KurinKey: group.KurinKey,
        Name: group.Name,
        Description: group.Description,
        SilhouetteUrl: links.For(group.SilhouetteBlobName),
        KurinNumber: group.Kurin?.Number);

    /// <summary>
    /// Renames the гурток and rewrites its description. Never the kurin: a гурток does not change
    /// kurin by being renamed, and the update path builds the command without a KurinKey, so
    /// writing it would put Guid.Empty over a live foreign key.
    /// </summary>
    public static void ApplyTo(this UpsertGroupCommand request, GroupEntity group, DateTime now)
    {
        group.Name = request.Name;
        group.Description = TextRules.TrimOrNull(request.Description);
        group.UpdatedDate = now;
    }
}
