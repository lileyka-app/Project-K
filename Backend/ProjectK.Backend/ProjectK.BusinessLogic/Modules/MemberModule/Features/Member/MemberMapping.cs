using ProjectK.BusinessLogic.Modules.MemberModule.Features.MemberAward;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.MemberWarning;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Rules.AuthModule;
using ProjectK.Common.Rules.KraiModule;
using ProjectK.Common.Rules.KurinModule;
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
    /// The гуртки their offices were held in come from the kurin module, keyed as
    /// <see cref="OfficeGroupKeys"/> asks.
    /// </summary>
    public static MemberDto ToDto(this MemberEntity member, IBlobReadLinks links, IReadOnlyDictionary<Guid, GroupSummary> officeGroups) => new()
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
        LeadershipHistories = member.LeadershipHistories.Select(history => history.ToDto(officeGroups)).ToList(),
        Warnings = member.MemberWarnings.Select(MemberWarningMapping.ToDto).ToList(),
        Awards = member.MemberAwards.Select(MemberAwardMapping.ToDto).ToList(),
        ProfilePhotoUrl = links.For(member.ProfilePhotoBlobName),
        ProfileVerificationStatus = member.ProfileVerificationStatus,
        ProfileVerifiedAtUtc = member.ProfileVerifiedAtUtc,
        ProfileVerifiedByUserKey = member.ProfileVerifiedByUserKey,
        ProfileVerificationNote = member.ProfileVerificationNote
    };

    /// <summary>
    /// A roster row in the shape of the full card, with what the row only holds as facts decided
    /// here: кадра by <see cref="KurinRosterRules"/> — the same rule the звіт куреня applies — the
    /// role by the first office held in this kurin, the level by the newest ступінь. A list never
    /// carries anyone's public code — it is answered for a whole kurin, and the code is something one
    /// person hands over — nor parents' contacts or awards, which are read on one person's card.
    /// </summary>
    public static MemberDto ToDto(this MemberListRow row, IBlobReadLinks links)
    {
        var mentored = row.Assignments
            .Where(assignment => KurinRosterRules.IsStaffAssignment(assignment.RevokedAtUtc, assignment.GroupKurinKey, row.KurinKey))
            .Select(assignment => assignment.GroupName)
            .ToList();
        var naming = row.Offices
            .OrderBy(office => office.Type)
            .ThenBy(office => office.Role)
            .FirstOrDefault();

        return new MemberDto
        {
            MemberKey = row.MemberKey,
            GroupKey = row.GroupKey ?? Guid.Empty,
            GroupName = row.GroupName,
            KurinKey = row.KurinKey,
            IsStaff = mentored.Count > 0
                || row.Offices.Any(office => KurinRosterRules.IsStaffOffice(
                    office.Type, office.OfficeKurinKey, office.OfficeEndDate, office.EndDate, row.KurinKey)),
            MentoredGroupNames = mentored,
            UserKey = row.UserKey,
            UserRole = naming is null ? null : SystemRoleNames.ForOffice(naming.Type, naming.Role),
            FirstName = row.FirstName,
            MiddleName = row.MiddleName,
            LastName = row.LastName,
            Email = row.Email,
            PhoneNumber = row.PhoneNumber,
            DateOfBirth = row.DateOfBirth,
            Address = row.Address,
            School = row.School,
            LatestPlastLevel = LatestLevel(row.PlastLevels, row.StoredPlastLevel),
            // Without the catalogue id the card carries: the list has never sent it.
            PlastLevelHistories = row.PlastLevels
                .Select(history => new PlastLevelHistoryDto
                {
                    MemberKey = history.MemberKey,
                    PlastLevelHistoryKey = history.PlastLevelHistoryKey,
                    PlastLevel = history.PlastLevel,
                    DateAchieved = history.DateAchieved,
                    IsPlastunka = history.IsPlastunka
                })
                .ToList(),
            LeadershipHistories = row.Offices
                .Select(office => new LeadershipHistoryDto
                {
                    LeadershipHistoryKey = office.LeadershipHistoryKey,
                    MemberKey = office.MemberKey,
                    LeadershipKey = office.LeadershipKey,
                    Role = office.Role,
                    LeadershipType = office.Type,
                    GroupName = office.GroupName,
                    KurinKey = row.KurinKey,
                    StartDate = office.StartDate,
                    EndDate = office.EndDate
                })
                .ToList(),
            Warnings = row.Warnings.Select(MemberWarningMapping.ToDto).ToList(),
            ProfilePhotoUrl = links.For(row.ProfilePhotoBlobName),
            ProfileVerificationStatus = row.ProfileVerificationStatus,
            ProfileVerifiedAtUtc = row.ProfileVerifiedAtUtc,
            ProfileVerifiedByUserKey = row.ProfileVerifiedByUserKey,
            ProfileVerificationNote = row.ProfileVerificationNote
        };
    }

    public static PlastLevelHistoryDto ToDto(this PlastLevelHistory history) => new()
    {
        MemberKey = history.MemberKey,
        PlastLevelHistoryKey = history.PlastLevelHistoryKey,
        PlastLevel = history.PlastLevel,
        DateAchieved = history.DateAchieved,
        IsPlastunka = history.IsPlastunka,
        CatalogId = PlastLevelDegreeRules.CatalogId(history.PlastLevel, history.IsPlastunka)
    };

    /// <summary>The гуртки the person's offices were held in, to ask the kurin module about.</summary>
    public static IReadOnlyCollection<Guid> OfficeGroupKeys(MemberEntity member) =>
        member.LeadershipHistories
            .Select(history => history.Leadership?.GroupKey)
            .OfType<Guid>()
            .Distinct()
            .ToArray();

    /// <summary>An office the person held, named by its провід: the type, the гурток and the kurin it is in.</summary>
    public static LeadershipHistoryDto ToDto(this LeadershipHistory history, IReadOnlyDictionary<Guid, GroupSummary> officeGroups)
    {
        var group = history.Leadership?.GroupKey is { } groupKey ? officeGroups.GetValueOrDefault(groupKey) : null;
        return new LeadershipHistoryDto
        {
            LeadershipHistoryKey = history.LeadershipHistoryKey,
            MemberKey = history.MemberKey,
            LeadershipKey = history.LeadershipKey,
            Role = history.Role,
            LeadershipType = history.Leadership?.Type,
            GroupName = group?.Name,
            KurinKey = history.Leadership?.KurinKey ?? group?.KurinKey,
            StartDate = history.StartDate,
            EndDate = history.EndDate
        };
    }

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
    /// A person in a picker, named by the office the read found them in. The photo stays a blob name
    /// until the caller signs it (<see cref="MemberLookupPhotoRules"/>); verification is not read.
    /// </summary>
    public static MemberLookupDto ToLookupDto(this MemberLookupRow row) => new()
    {
        MemberKey = row.MemberKey,
        UserKey = row.UserKey,
        FirstName = row.FirstName,
        MiddleName = row.MiddleName,
        LastName = row.LastName,
        ProfilePhotoBlobName = row.ProfilePhotoBlobName,
        UserRole = row.Office is null ? null : SystemRoleNames.ForOffice(row.Office.Type, row.Office.Role)
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

    /// <summary>The newest ступінь on record; a record older than the history holds it alone.</summary>
    private static PlastLevel? LatestLevel(IEnumerable<PlastLevelHistory> history, PlastLevel? stored) =>
        history
            .OrderByDescending(entry => entry.DateAchieved)
            .Select(entry => (PlastLevel?)entry.PlastLevel)
            .FirstOrDefault() ?? stored;
}
