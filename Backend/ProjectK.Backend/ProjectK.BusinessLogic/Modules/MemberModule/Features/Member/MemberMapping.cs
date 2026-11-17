using ProjectK.BusinessLogic.Modules.MemberModule.Features.MemberAward;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.MemberWarning;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Rules.KraiModule;
using ProjectK.Common.Rules.MemberModule;
using ProjectK.Common.Rules.Shared;
using MemberEntity = ProjectK.Common.Entities.MemberModule.Member;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;

/// <summary>A person's card and its pieces as the client reads them, and the profile write onto the record.</summary>
public static class MemberMapping
{
    /// <summary>
    /// The person's own record. Where they stand, what they run, their account and their tags live
    /// elsewhere, so the read that knows them fills those afterwards; the mask fills the birthday.
    /// The offices they held come from the kurin module (<c>IOfficeDirectory.GetHeldByMemberAsync</c>).
    /// </summary>
    public static MemberDto ToDto(this MemberEntity member, IBlobReadLinks links, IReadOnlyList<LeadershipHistoryDto> offices) => new()
    {
        MemberKey = member.MemberKey,
        PublicId = member.PublicId,
        UserKey = member.UserKey,
        FirstName = member.FirstName,
        MiddleName = member.MiddleName,
        LastName = member.LastName,
        Email = member.Email,
        PhoneNumber = member.PhoneNumber,
        DateOfBirth = member.DateOfBirth,
        Address = member.Address,
        School = member.School,
        MotherFirstName = member.MotherFirstName,
        MotherLastName = member.MotherLastName,
        MotherPhone = member.MotherPhone,
        FatherFirstName = member.FatherFirstName,
        FatherLastName = member.FatherLastName,
        FatherPhone = member.FatherPhone,
        CardExtras = MemberCardExtraRules.Read(member.CardExtras),
        LatestPlastLevel = LatestLevel(member.PlastLevelHistory, member.LatestPlastLevel),
        PlastLevelHistories = member.PlastLevelHistory.Select(ToDto).ToList(),
        LeadershipHistories = offices.ToList(),
        Warnings = member.MemberWarnings.Select(MemberWarningMapping.ToDto).ToList(),
        Awards = member.MemberAwards.Select(MemberAwardMapping.ToDto).ToList(),
        ProfilePhotoUrl = links.For(member.ProfilePhotoBlobName),
        ProfileVerificationStatus = member.ProfileVerificationStatus,
        ProfileVerifiedAtUtc = member.ProfileVerifiedAtUtc,
        ProfileVerifiedByUserKey = member.ProfileVerifiedByUserKey,
        ProfileVerificationNote = member.ProfileVerificationNote
    };

    public static PlastLevelHistoryDto ToDto(this PlastLevelHistory history) => new()
    {
        MemberKey = history.MemberKey,
        PlastLevelHistoryKey = history.PlastLevelHistoryKey,
        PlastLevel = history.PlastLevel,
        DateAchieved = history.DateAchieved,
        IsPlastunka = history.IsPlastunka,
        CatalogId = PlastLevelDegreeRules.CatalogId(history.PlastLevel, history.IsPlastunka)
    };

    /// <summary>A person in a picker. Which office they hold is the reader's to say, so <c>UserRole</c> stays empty.</summary>
    public static MemberLookupDto ToLookupDto(this MemberEntity member, IBlobReadLinks links) => new()
    {
        MemberKey = member.MemberKey,
        UserKey = member.UserKey,
        FirstName = member.FirstName,
        MiddleName = member.MiddleName,
        LastName = member.LastName,
        ProfileVerificationStatus = member.ProfileVerificationStatus,
        ProfileVerifiedAtUtc = member.ProfileVerifiedAtUtc,
        ProfileVerifiedByUserKey = member.ProfileVerifiedByUserKey,
        ProfileVerificationNote = member.ProfileVerificationNote,
        ProfilePhotoBlobName = member.ProfilePhotoBlobName,
        ProfilePhotoUrl = links.For(member.ProfilePhotoBlobName)
    };

    /// <summary>
    /// Masks the person's data for the viewer and clears their public code unless it is their own:
    /// leadership that knew it could sign them into another kurin without ever asking them.
    /// </summary>
    public static MemberDto MaskFor(this MemberDto member, MemberFieldMask mask, Guid? groupKey)
    {
        mask.Apply(member, member.UserKey, groupKey);
        if (!mask.IsSelf(member.UserKey))
        {
            member.PublicId = null;
        }

        return member;
    }

    /// <summary>
    /// The member's own data out of the form the UI submits. The photo and the account are separate
    /// use cases, so their fields stay behind.
    /// </summary>
    public static UpsertMemberProfileCommand ToProfileCommand(this UpsertMemberCommand request) => new UpsertMemberProfileCommand
    {
        MemberKey = request.MemberKey,
        KurinKey = request.KurinKey,
        GroupKey = request.GroupKey,
        FirstName = request.FirstName,
        MiddleName = request.MiddleName,
        LastName = request.LastName,
        Email = request.Email,
        PhoneNumber = request.PhoneNumber,
        DateOfBirth = request.DateOfBirth,
        Address = request.Address,
        School = request.School,
        PlastLevelHistories = request.PlastLevelHistories,
        CardExtras = request.CardExtras,
        ApplyCardFields = request.ApplyCardFields
    }.WithParentsOf(request);

    /// <summary>
    /// Writes the profile the request carries onto the record. Only these fields: the key, the
    /// account, the photo, the verification, the ступені and the край's own fields are each written
    /// by whoever owns them, so a new field of the request reaches the record only once listed here.
    /// </summary>
    public static void ApplyTo(this UpsertMemberProfileCommand request, MemberEntity member, DateTime now)
    {
        member.FirstName = request.FirstName;
        member.MiddleName = request.MiddleName;
        member.LastName = request.LastName;
        member.Email = request.Email;
        member.PhoneNumber = request.PhoneNumber;
        member.DateOfBirth = request.DateOfBirth;
        member.Address = request.Address;
        member.School = request.School;
        member.MotherFirstName = request.MotherFirstName;
        member.MotherLastName = request.MotherLastName;
        member.MotherPhone = request.MotherPhone;
        member.FatherFirstName = request.FatherFirstName;
        member.FatherLastName = request.FatherLastName;
        member.FatherPhone = request.FatherPhone;
        member.UpdatedDate = now;
    }

    /// <summary>The newest ступінь on record; a record older than the history holds it alone.</summary>
    private static PlastLevel? LatestLevel(IEnumerable<PlastLevelHistory> history, PlastLevel? stored) =>
        history
            .OrderByDescending(entry => entry.DateAchieved)
            .Select(entry => (PlastLevel?)entry.PlastLevel)
            .FirstOrDefault() ?? stored;
}
