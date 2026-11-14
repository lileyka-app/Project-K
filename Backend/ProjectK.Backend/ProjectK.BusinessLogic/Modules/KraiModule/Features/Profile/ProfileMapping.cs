using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Models.Dtos.KraiModule;
using ProjectK.Common.Models.KraiProfiles;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Features.Profile;

internal static class ProfileMapping
{
    public static KraiProfileEditorDto ToEditorDto(this Krai krai, int revision, KraiProfileDocument document) => new(
        KraiCode: krai.Code,
        KraiName: krai.Name,
        Revision: revision,
        Document: document);

    /// <summary>A revision for the history list, under <paramref name="authorName"/>: who saved it, or <c>null</c> when unknown.</summary>
    public static KraiProfileRevisionDto ToDto(this KraiProfileRevision revision, string? authorName) => new(
        Revision: revision.Revision,
        CreatedAtUtc: revision.CreatedAtUtc,
        CreatedByName: authorName,
        Note: revision.Note);

    /// <summary>The fields a kurin may hide, in the profile's order, each marked as it stands in <paramref name="hiddenJson"/>.</summary>
    public static IReadOnlyList<KurinCardFieldDto> ToCardFieldDtos(this KraiCardFieldsSettings card, string? hiddenJson)
    {
        var hidden = KurinHiddenCardFields.Read(hiddenJson);
        return card.Hideable().Select(field => new KurinCardFieldDto(field.Key, field.Label, hidden.Contains(field.Key))).ToList();
    }
}
