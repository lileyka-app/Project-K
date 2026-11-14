using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Models.Dtos.KurinModule;
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
        group.Description = Trimmed(request.Description);
        group.UpdatedDate = now;
    }

    /// <summary>Free text as it is kept: trimmed, and blank as nothing.</summary>
    public static string? Trimmed(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
