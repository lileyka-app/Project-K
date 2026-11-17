using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Rules.IdentityModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Registry;

/// <summary>A kurin's roster row as the registry screen and file read it.</summary>
public static class RegistryMapping
{
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
            Warnings = row.Warnings.Select(ToDto).ToList(),
            ProfilePhotoUrl = links.For(row.ProfilePhotoBlobName),
            ProfileVerificationStatus = row.ProfileVerificationStatus,
            ProfileVerifiedAtUtc = row.ProfileVerifiedAtUtc,
            ProfileVerifiedByUserKey = row.ProfileVerifiedByUserKey,
            ProfileVerificationNote = row.ProfileVerificationNote
        };
    }

    /// <summary>An open пересторога on a roster row, as the card would show it.</summary>
    private static MemberWarningDto ToDto(MemberWarning warning) => new()
    {
        MemberWarningKey = warning.MemberWarningKey,
        MemberKey = warning.MemberKey,
        Level = warning.Level,
        IssuedAtUtc = warning.IssuedAtUtc,
        ExpiresAtUtc = warning.ExpiresAtUtc,
        IssuedByUserKey = warning.IssuedByUserKey,
        RevokedByUserKey = warning.RevokedByUserKey,
        RevokedAtUtc = warning.RevokedAtUtc
    };

    /// <summary>The newest ступінь on record; a record older than the history holds it alone.</summary>
    private static PlastLevel? LatestLevel(IEnumerable<PlastLevelHistory> history, PlastLevel? stored) =>
        history
            .OrderByDescending(entry => entry.DateAchieved)
            .Select(entry => (PlastLevel?)entry.PlastLevel)
            .FirstOrDefault() ?? stored;
}
