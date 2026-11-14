using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberWarning;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Rules.KraiModule;
using ProjectK.Common.Rules.KurinModule;
using ProjectK.Common.Rules.Shared;
using MemberEntity = ProjectK.Common.Entities.KurinModule.Member;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member;

/// <summary>A person's card and its pieces as the client reads them, and the profile write onto the record.</summary>
public static class MemberMapping
{
    /// <summary>
    /// The person's own record. Where they stand, what they run, their account and their tags live
    /// elsewhere, so the read that knows them fills those afterwards; the mask fills the birthday.
    /// </summary>
    public static MemberDto ToDto(this MemberEntity member, IBlobReadLinks links) => new()
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
        CardExtras = MemberCardExtras.Read(member.CardExtras),
        // The newest ступінь on record; a record older than the history holds it alone.
        LatestPlastLevel = member.PlastLevelHistory
            .OrderByDescending(history => history.DateAchieved)
            .Select(history => (PlastLevel?)history.PlastLevel)
            .FirstOrDefault() ?? member.LatestPlastLevel,
        PlastLevelHistories = member.PlastLevelHistory.Select(ToDto).ToList(),
        LeadershipHistories = member.LeadershipHistories.Select(ToDto).ToList(),
        Warnings = member.MemberWarnings.Select(MemberWarningMapping.ToDto).ToList(),
        Awards = member.MemberAwards.Select(MemberAwardMapping.ToDto).ToList(),
        ProfilePhotoUrl = links.For(member.ProfilePhotoBlobName),
        ProfileVerificationStatus = member.ProfileVerificationStatus,
        ProfileVerifiedAtUtc = member.ProfileVerifiedAtUtc,
        ProfileVerifiedByUserKey = member.ProfileVerifiedByUserKey,
        ProfileVerificationNote = member.ProfileVerificationNote
    };

    /// <summary>
    /// A roster row in the shape of the full card. Level, open offices and open перестороги are
    /// already resolved by the projection. A list never carries anyone's public code — it is answered
    /// for a whole kurin, and the code is something one person hands over — nor parents' contacts or
    /// awards, which are read on one person's card.
    /// </summary>
    public static MemberDto ToDto(this MemberListItemDto row, IBlobReadLinks links) => new()
    {
        MemberKey = row.MemberKey,
        GroupKey = row.GroupKey ?? Guid.Empty,
        GroupName = row.GroupName,
        KurinKey = row.KurinKey,
        IsStaff = row.IsStaff,
        MentoredGroupNames = [.. row.MentoredGroupNames],
        UserKey = row.UserKey,
        UserRole = row.UserRole,
        FirstName = row.FirstName,
        MiddleName = row.MiddleName,
        LastName = row.LastName,
        Email = row.Email,
        PhoneNumber = row.PhoneNumber,
        DateOfBirth = row.DateOfBirth,
        Birthday = row.Birthday,
        HiddenData = row.HiddenData,
        Address = row.Address,
        School = row.School,
        LatestPlastLevel = row.LatestPlastLevel,
        PlastLevelHistories = [.. row.PlastLevelHistories],
        LeadershipHistories = [.. row.LeadershipHistories],
        Warnings = [.. row.Warnings],
        ProfilePhotoUrl = links.For(row.ProfilePhotoBlobName),
        ProfileVerificationStatus = row.ProfileVerificationStatus,
        ProfileVerifiedAtUtc = row.ProfileVerifiedAtUtc,
        ProfileVerifiedByUserKey = row.ProfileVerifiedByUserKey,
        ProfileVerificationNote = row.ProfileVerificationNote
    };

    public static PlastLevelHistoryDto ToDto(this PlastLevelHistory history) => new()
    {
        MemberKey = history.MemberKey,
        PlastLevelHistoryKey = history.PlastLevelHistoryKey,
        PlastLevel = history.PlastLevel,
        DateAchieved = history.DateAchieved,
        IsPlastunka = history.IsPlastunka,
        CatalogId = PlastLevelDegrees.CatalogId(history.PlastLevel, history.IsPlastunka)
    };

    /// <summary>An office the person held, named by its провід: the type, the гурток and the kurin it is in.</summary>
    public static LeadershipHistoryDto ToDto(this LeadershipHistory history) => new()
    {
        LeadershipHistoryKey = history.LeadershipHistoryKey,
        MemberKey = history.MemberKey,
        LeadershipKey = history.LeadershipKey,
        Role = history.Role,
        LeadershipType = history.Leadership?.Type,
        GroupName = history.Leadership?.Group?.Name,
        KurinKey = history.Leadership?.KurinKey ?? history.Leadership?.Group?.KurinKey,
        StartDate = history.StartDate,
        EndDate = history.EndDate
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
    /// A person another module handed back as a summary. The summary carries no middle name and no
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
}
