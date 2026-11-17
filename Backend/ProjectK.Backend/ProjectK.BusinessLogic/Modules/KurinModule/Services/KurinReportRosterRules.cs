using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Rules.KurinModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// Who is where in the звіт куреня: which гурток a person is in, who counts as кадра, how the youth
/// spread over the ladder, and the names the report puts on a person, a гурток's виховники and a role.
/// Each answer is the one the реєстр gives for the same people.
/// </summary>
public static class KurinReportRosterRules
{
    /// <summary>Which гурток of this kurin the person is in — their membership says, not their record.</summary>
    public static Guid? GroupOf(KurinReportSourceData source, Guid memberKey)
        => source.MembershipByMemberKey.TryGetValue(memberKey, out var membership) ? membership.GroupKey : null;

    /// <summary>
    /// Ступінь, який людина має зараз: найновіший записаний, а як історії немає — збережене поле.
    /// Дзеркалить читання списку учасників. Брати саме <c>LatestPlastLevel</c> не можна: воно
    /// оновлюється записом, і звіт показував би не те, що реєстр, у кожного, кому ступінь додали
    /// заднім числом.
    /// </summary>
    public static PlastLevel? LatestLevelOf(KurinReportSourceMember member)
        => member.Levels
            .OrderByDescending(history => history.DateAchieved)
            .Select(history => (PlastLevel?)history.PlastLevel)
            .FirstOrDefault() ?? member.LatestPlastLevel;

    /// <summary>
    /// Чи ця роль узагалі про цей курінь. Курінні й КВ-ролі носять курінь на собі, гурткові —
    /// через гурток, і `groupNamesByKey` — це рівно гуртки цього куреня.
    /// <para>
    /// Питати доводиться тому, що ролі читаються разом із людиною, а людина від 0.20 може бути в
    /// кількох куренях: без цієї перевірки роль виховника в курені А друкувалася б у звіті куреня Б.
    /// </para>
    /// </summary>
    public static bool BelongsToKurin(
        KurinReportSourceOffice office,
        Guid kurinKey,
        IReadOnlyDictionary<Guid, string> groupsHere)
        => office.Type == LeadershipType.Group
            ? office.GroupKey is Guid groupKey && groupsHere.ContainsKey(groupKey)
            : office.KurinKey == kurinKey;

    /// <summary>The names of the гуртки each виховник is assigned to, by their account.</summary>
    public static Dictionary<Guid, IReadOnlyList<string>> MentoredGroupsByUser(
        IEnumerable<KurinReportSourceMentor> mentorAssignments,
        IReadOnlyDictionary<Guid, string> groupNamesByKey)
    {
        // Де закріплений виховник — питається в закріплень, а не в його власного членства: воно
        // ставить його в курінь і зазвичай у жоден гурток. Так само, як це читає реєстр.
        return mentorAssignments
            .GroupBy(assignment => assignment.MentorUserKey)
            .ToDictionary(
                assignments => assignments.Key,
                assignments => (IReadOnlyList<string>)assignments
                    .Select(assignment => groupNamesByKey.GetValueOrDefault(assignment.GroupKey))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!)
                    .Distinct(StringComparer.CurrentCulture)
                    .OrderBy(name => name, StringComparer.CurrentCulture)
                    .ToArray());
    }

    /// <summary>The members of this kurin who count as кадра rather than youth.</summary>
    public static HashSet<Guid> StaffKeys(
        IEnumerable<KurinReportSourceMember> members,
        IReadOnlyList<KurinReportSourceMentor> mentorAssignments,
        IEnumerable<Group> groups,
        Guid kurinKey)
    {
        // Кадра — за тим самим правилом, що й у реєстрі, і воно живе в одному місці на обидва
        // виводи. Раніше сюди потрапляв і курінний, і будь-хто з глобальною роллю в Identity —
        // тобто людина, яка має роль виховника в іншому курені, рахувалась кадрою й тут. А того,
        // хто лише закріплений за гуртком, звіт не рахував зовсім — реєстр і сайдбар рахували.
        var kurinOfGroup = groups.ToDictionary(group => group.GroupKey, group => group.KurinKey);
        return members
            .Where(member => member.Offices.Any(office => KurinRosterRules.IsStaffOffice(
                    office.Type,
                    office.KurinKey,
                    office.OfficeEndDate,
                    office.EndDate,
                    kurinKey))
                || (member.UserKey is Guid accountKey && mentorAssignments.Any(assignment =>
                    assignment.MentorUserKey == accountKey
                    && KurinRosterRules.IsStaffAssignment(
                        assignment.RevokedAtUtc,
                        kurinOfGroup.GetValueOrDefault(assignment.GroupKey),
                        kurinKey))))
            .Select(member => member.MemberKey)
            .ToHashSet();
    }

    /// <summary>
    /// Скільки юнаків стоїть на кожному ступені драбини цієї гілки. Впорядники не рахуються — вони
    /// кадра, а не склад юнацтва; рядок «Без ступеня / інший» є завжди, коли є кого в нього
    /// покласти, інакше стовпчик не сходився б зі складом. Те саме, що показує реєстр.
    /// </summary>
    public static IReadOnlyList<KurinReportLevelCount> LevelTally(
        KurinBranch branch,
        IReadOnlyList<KurinReportMember> youth)
    {
        var ladder = PlastLadderRules.DefaultFor(branch);
        var rows = ladder
            .Select(level => new KurinReportLevelCount(
                PlastLevelNames.Of(level),
                youth.Count(member => member.LatestPlastLevel == level)))
            .ToList();

        var onLadder = ladder.ToHashSet();
        var rest = youth.Count(member =>
            member.LatestPlastLevel is null || !onLadder.Contains(member.LatestPlastLevel.Value));
        if (rest > 0)
        {
            rows.Add(new KurinReportLevelCount("Без ступеня / інший", rest));
        }

        rows.Add(new KurinReportLevelCount("Разом", youth.Count));
        return rows;
    }

    /// <summary>The виховники of a гурток by name: as a member if they are one here, else by their account.</summary>
    public static IReadOnlyList<string> MentorNames(
        Guid groupKey,
        IEnumerable<KurinReportSourceMentor> mentorAssignments,
        IReadOnlyDictionary<Guid, KurinReportSourceMember> memberByUserKey,
        IReadOnlyDictionary<Guid, AppUser> usersByKey)
    {
        return mentorAssignments
            .Where(assignment => assignment.GroupKey == groupKey)
            .Select(assignment =>
            {
                if (memberByUserKey.TryGetValue(assignment.MentorUserKey, out var member))
                {
                    return PersonNames.Full(member.FirstName, member.MiddleName, member.LastName);
                }

                if (usersByKey.TryGetValue(assignment.MentorUserKey, out var user))
                {
                    return PersonNames.FirstLast(user.FirstName, user.LastName);
                }

                return assignment.MentorUserKey.ToString();
            })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToArray();
    }

    /// <summary>The first letters of the person's names, upper case.</summary>
    public static string Initials(KurinReportSourceMember member)
        => string.Concat(new[] { member.FirstName, member.MiddleName, member.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => char.ToUpperInvariant(part![0])));

    /// <summary>What a role is held over, beside its type; null when the type already says it.</summary>
    public static string? LeadershipScopeName(
        KurinReportSourceOffice leadership,
        IReadOnlyDictionary<Guid, string> groupNamesByKey)
    {
        if (!string.IsNullOrWhiteSpace(leadership.Name))
        {
            return leadership.Name;
        }

        // Курінні й КВ-ролі нічим не звужені — звіт і так про цей курінь, а рядок поруч уже
        // каже «Курінь» чи «КВ». Раніше тут стояли літерали "Kurin" і "KV", тож у документі виходило
        // «КВ/Впорядник; KV». Гурток без назви — так само нічого, а не його guid.
        return leadership.Type == LeadershipType.Group && leadership.GroupKey is Guid groupKey
            ? groupNamesByKey.GetValueOrDefault(groupKey)
            : null;
    }
}
