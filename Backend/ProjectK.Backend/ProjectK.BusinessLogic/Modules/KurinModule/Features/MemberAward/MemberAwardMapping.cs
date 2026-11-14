using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using MemberAwardEntity = ProjectK.Common.Entities.KurinModule.MemberAward;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward;

/// <summary>A відзначення as the card reads it.</summary>
public static class MemberAwardMapping
{
    /// <summary>
    /// An УПЮ відзначення carries the link to its picture, coloured once confirmed; a senior one is
    /// drawn by the client from the catalogue and has none.
    /// </summary>
    public static MemberAwardDto ToDto(this MemberAwardEntity award) => new()
    {
        MemberAwardKey = award.MemberAwardKey,
        MemberKey = award.MemberKey,
        KurinKey = award.KurinKey,
        Level = award.Level,
        CatalogId = award.CatalogId,
        DateAcquired = award.DateAcquired,
        Note = award.Note,
        IsPinned = award.IsPinned,
        Status = award.Status,
        ImageUrl = award.Level is null
            ? null
            : $"/api/awards/images/{(int)award.Level}?colored={award.Status == BadgeProgressStatus.Confirmed}",
        SubmittedAtUtc = award.SubmittedAtUtc,
        SubmittedByUserKey = award.SubmittedByUserKey,
        ReviewedAtUtc = award.ReviewedAtUtc,
        ReviewedByUserKey = award.ReviewedByUserKey
    };
}
