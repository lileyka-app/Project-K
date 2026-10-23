using Microsoft.Extensions.Configuration;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Reports;
using ProjectK.Common.Models.Roster;
using ProjectK.Common.Models.Settings;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

public sealed class KurinReportDataService
{
    private readonly IKurinReportSource _source;
    private readonly ICurrentUserContext _currentUser;
    private readonly IBlobReadLinks _links;
    private readonly IKurinReportMedia _media;
    private readonly IProbesCatalogService _probesCatalogService;
    private readonly IBadgesCatalogService _badgesCatalogService;
    private readonly IConfiguration _configuration;
    // What people have earned belongs to another module; the report asks for it, all at once.
    private readonly IMemberProgressDirectory _progress;

    public KurinReportDataService(
        IKurinReportSource source,
        ICurrentUserContext currentUser,
        IBlobReadLinks links,
        IKurinReportMedia media,
        IProbesCatalogService probesCatalogService,
        IBadgesCatalogService badgesCatalogService,
        IConfiguration configuration,
        IMemberProgressDirectory progress)
    {
        _source = source;
        _currentUser = currentUser;
        _links = links;
        _media = media;
        _probesCatalogService = probesCatalogService;
        _badgesCatalogService = badgesCatalogService;
        _configuration = configuration;
        _progress = progress;
    }

    public async Task<KurinReportData?> BuildAsync(Guid kurinKey, CancellationToken cancellationToken)
    {
        var source = await _source.LoadAsync(kurinKey, _currentUser.UserId, cancellationToken);
        if (source is null)
        {
            return null;
        }

        var (kurin, groups, mentorAssignments, members, usersByKey, _) = source;
        var progressByMember = await _progress.GetDetailsForMembersAsync(
            members.Select(member => member.MemberKey).ToArray(),
            cancellationToken);

        var groupNamesByKey = groups.ToDictionary(group => group.GroupKey, group => group.Name);
        var memberByUserKey = members
            .Where(member => member.UserKey.HasValue)
            .GroupBy(member => member.UserKey!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        // Де закріплений виховник — питається в закріплень, а не в його власного членства: воно
        // ставить його в курінь і зазвичай у жоден гурток. Так само, як це читає реєстр.
        var mentoredGroupsByUserKey = mentorAssignments
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

        var reportMembers = new List<KurinReportMember>(members.Count);
        foreach (var member in members)
        {
            reportMembers.Add(await BuildMemberReportAsync(
                member,
                source,
                progressByMember.GetValueOrDefault(member.MemberKey) ?? MemberProgressDetail.Empty,
                kurinKey,
                groupNamesByKey,
                mentoredGroupsByUserKey,
                cancellationToken));
        }

        var reportGroups = new List<KurinReportGroup>(groups.Count);
        foreach (var group in groups)
        {
            reportGroups.Add(new KurinReportGroup(
                group.GroupKey,
                group.Name,
                group.Description,
                _links.For(group.SilhouetteBlobName),
                await _media.TryDownloadAsync(group.SilhouetteBlobName, cancellationToken),
                ResolveMentorNames(group.GroupKey, mentorAssignments, memberByUserKey, usersByKey),
                members
                    .Where(member => GroupOf(source, member.MemberKey) == group.GroupKey)
                    .OrderBy(member => member.LastName)
                    .ThenBy(member => member.FirstName)
                    .Select(member => new KurinReportGroupMember(
                        member.MemberKey,
                        BuildFullName(member),
                        member.Email,
                        member.PhoneNumber,
                        LatestLevelOf(member)))
                    .ToArray()));
        }

        // Кадра — за тим самим правилом, що й у реєстрі, і воно живе в одному місці на обидва
        // виводи. Раніше сюди потрапляв і курінний, і будь-хто з глобальною роллю в Identity —
        // тобто людина, яка має роль виховника в іншому курені, рахувалась кадрою й тут. А того,
        // хто лише закріплений за гуртком, звіт не рахував зовсім — реєстр і сайдбар рахували.
        var kurinOfGroup = groups.ToDictionary(group => group.GroupKey, group => group.KurinKey);
        var staffKeys = members
            .Where(member => member.LeadershipHistories.Any(history => KurinRoster.IsStaffOffice(
                    history.Leadership.Type,
                    history.Leadership.KurinKey,
                    history.Leadership.EndDate,
                    history.EndDate,
                    kurinKey))
                || (member.UserKey is Guid accountKey && mentorAssignments.Any(assignment =>
                    assignment.MentorUserKey == accountKey
                    && KurinRoster.IsStaffAssignment(
                        assignment.RevokedAtUtc,
                        kurinOfGroup.GetValueOrDefault(assignment.GroupKey),
                        kurinKey))))
            .Select(member => member.MemberKey)
            .ToHashSet();

        var staff = reportMembers
            .Where(member => staffKeys.Contains(member.MemberKey))
            .OrderBy(member => member.FullName, StringComparer.CurrentCulture)
            .ToArray();

        var youth = reportMembers
            .Where(member => !staffKeys.Contains(member.MemberKey))
            .OrderBy(member => member.FullName, StringComparer.CurrentCulture)
            .ToArray();

        return new KurinReportData(
            new KurinReportHeader(
                DateTime.UtcNow,
                ResolveCurrentUserName(usersByKey),
                ResolveCurrentUserEmail(usersByKey),
                ResolveReleaseInfo("Version"),
                // Not through ResolveReleaseInfo: its "unknown" fallback would print as a code name.
                ReleaseDisplay.CodeName(_configuration["ReleaseInfo:Codename"] ?? _configuration["ReleaseInfo:CodeName"])),
            new KurinReportKurin(
                kurin.KurinKey,
                kurin.Number,
                kurin.Stanytsia,
                kurin.RegionOrCountry,
                kurin.NamedAfter,
                kurin.Description),
            reportGroups,
            staff,
            youth,
            BuildLevelTally(kurin.Branch, youth),
            reportMembers);
    }

    /// <summary>
    /// Скільки юнаків стоїть на кожному ступені драбини цієї гілки. Впорядники не рахуються — вони
    /// кадра, а не склад юнацтва; рядок «Без ступеня / інший» є завжди, коли є кого в нього
    /// покласти, інакше стовпчик не сходився б зі складом. Те саме, що показує реєстр.
    /// </summary>
    private static IReadOnlyList<KurinReportLevelCount> BuildLevelTally(
        KurinBranch branch,
        IReadOnlyList<KurinReportMember> youth)
    {
        var ladder = PlastLadder.DefaultFor(branch);
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

    /// <summary>
    /// Ступінь, який людина має зараз: найновіший записаний, а як історії немає — збережене поле.
    /// Дзеркалить читання списку учасників. Брати саме <c>LatestPlastLevel</c> не можна: воно
    /// оновлюється записом, і звіт показував би не те, що реєстр, у кожного, кому ступінь додали
    /// заднім числом.
    /// </summary>
    private static PlastLevel? LatestLevelOf(Member member)
        => member.PlastLevelHistory
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
    private static bool BelongsToKurin(
        Leadership office,
        Guid kurinKey,
        IReadOnlyDictionary<Guid, string> groupsHere)
        => office.Type == LeadershipType.Group
            ? office.GroupKey is Guid groupKey && groupsHere.ContainsKey(groupKey)
            : office.KurinKey == kurinKey;

    private async Task<KurinReportMember> BuildMemberReportAsync(
        Member member,
        KurinReportSourceData source,
        MemberProgressDetail progress,
        Guid kurinKey,
        IReadOnlyDictionary<Guid, string> groupNamesByKey,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> mentoredGroupsByUserKey,
        CancellationToken cancellationToken)
    {
        return new KurinReportMember(
            member.MemberKey,
            member.UserKey,
            GroupOf(source, member.MemberKey),
            GroupOf(source, member.MemberKey) is Guid groupKey && groupNamesByKey.TryGetValue(groupKey, out var groupName)
                ? groupName
                : null,
            BuildFullName(member),
            BuildInitials(member),
            member.Email,
            member.PhoneNumber,
            member.DateOfBirth,
            member.Address,
            member.School,
            _links.For(member.ProfilePhotoBlobName),
            await _media.TryDownloadAsync(member.ProfilePhotoBlobName, cancellationToken),
            LatestLevelOf(member),
            member.UserKey is Guid mentorUserKey
                && mentoredGroupsByUserKey.TryGetValue(mentorUserKey, out var mentoredGroups)
                ? mentoredGroups
                : [],
            member.PlastLevelHistory
                .OrderByDescending(item => item.DateAchieved)
                .Select(item => new KurinReportPlastLevel(item.PlastLevel, item.DateAchieved))
                .ToArray(),
            progress.Probes
                .OrderBy(item => item.ProbeId)
                .Select(item =>
                {
                    var probe = _probesCatalogService.GetGroupedProbeById(item.ProbeId);
                    return new KurinReportProbe(
                        item.ProbeId,
                        ResolveProbeTitle(item.ProbeId, probe),
                        item.Status,
                        KurinReportTerminology.ProbeStatus(item.Status),
                        item.CompletedAtUtc,
                        item.CompletedByName,
                        item.VerifiedAtUtc,
                        item.VerifiedByName);
                })
                .ToArray(),
            progress.SignedPoints
                .OrderBy(item => item.ProbeId)
                .ThenBy(item => item.PointId)
                .Select(item =>
                {
                    var probe = _probesCatalogService.GetGroupedProbeById(item.ProbeId);
                    return new KurinReportProbePoint(
                        item.ProbeId,
                        ResolveProbeTitle(item.ProbeId, probe),
                        item.PointId,
                        ResolveProbePointLabel(item.PointId, probe),
                        item.SignedAtUtc,
                        item.SignedByName,
                        item.SignedByRole);
                })
                .ToArray(),
            progress.ConfirmedBadges
                .OrderBy(item => item.BadgeId)
                .Select(item =>
                {
                    var badge = _badgesCatalogService.GetBadgeById(item.BadgeId);
                    return new KurinReportBadge(
                        item.BadgeId,
                        badge?.Title ?? item.BadgeId,
                        item.Status,
                        KurinReportTerminology.BadgeStatus(item.Status),
                        item.ReviewedAtUtc,
                        item.ReviewedByName,
                        item.ReviewedByRole);
                })
                .ToArray(),
            member.MemberWarnings
                .Where(item => item.RevokedAtUtc == null && item.ExpiresAtUtc >= DateTime.UtcNow)
                .OrderByDescending(item => item.IssuedAtUtc)
                .Select(item => new KurinReportWarning(
                    item.Level,
                    KurinReportTerminology.WarningLevel(item.Level),
                    item.IssuedAtUtc,
                    item.ExpiresAtUtc,
                    item.RevokedAtUtc.HasValue))
                .ToArray(),
            member.MemberAwards
                .OrderByDescending(item => item.DateAcquired)
                .Select(item => new KurinReportAward(
                    item.Level,
                    KurinReportTerminology.AwardLevel(item.Level),
                    item.DateAcquired,
                    item.Note,
                    item.Status,
                    KurinReportTerminology.BadgeStatus(item.Status)))
                .ToArray(),
            member.LeadershipHistories
                .Where(item => BelongsToKurin(item.Leadership, kurinKey, groupNamesByKey))
                .OrderByDescending(item => item.StartDate)
                .Select(item => new KurinReportLeadershipHistory(
                    item.Leadership.Type,
                    KurinReportTerminology.LeadershipType(item.Leadership.Type),
                    item.Role,
                    KurinReportTerminology.LeadershipRole(item.Role),
                    ResolveLeadershipScopeName(item.Leadership, groupNamesByKey),
                    item.StartDate,
                    item.EndDate))
                .ToArray());
    }

    private static IReadOnlyList<string> ResolveMentorNames(
        Guid groupKey,
        IEnumerable<MentorAssignment> mentorAssignments,
        IReadOnlyDictionary<Guid, Member> memberByUserKey,
        IReadOnlyDictionary<Guid, Common.Entities.AuthModule.AppUser> usersByKey)
    {
        return mentorAssignments
            .Where(assignment => assignment.GroupKey == groupKey)
            .Select(assignment =>
            {
                if (memberByUserKey.TryGetValue(assignment.MentorUserKey, out var member))
                {
                    return BuildFullName(member);
                }

                if (usersByKey.TryGetValue(assignment.MentorUserKey, out var user))
                {
                    return $"{user.FirstName} {user.LastName}".Trim();
                }

                return assignment.MentorUserKey.ToString();
            })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToArray();
    }

    private string ResolveCurrentUserName(IReadOnlyDictionary<Guid, Common.Entities.AuthModule.AppUser> usersByKey)
    {
        if (_currentUser.UserId is Guid userKey && usersByKey.TryGetValue(userKey, out var user))
        {
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            return string.IsNullOrWhiteSpace(fullName) ? user.Email ?? user.UserName ?? user.Id.ToString() : fullName;
        }

        return "Unknown user";
    }

    private string? ResolveCurrentUserEmail(IReadOnlyDictionary<Guid, Common.Entities.AuthModule.AppUser> usersByKey)
    {
        return _currentUser.UserId is Guid userKey && usersByKey.TryGetValue(userKey, out var user)
            ? user.Email
            : null;
    }

    private string ResolveReleaseInfo(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _configuration[$"ReleaseInfo:{key}"];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "unknown";
    }
    /// <summary>Which гурток of this kurin the person is in — their membership says, not their record.</summary>
    private static Guid? GroupOf(KurinReportSourceData source, Guid memberKey)
        => source.MembershipByMemberKey.TryGetValue(memberKey, out var membership) ? membership.GroupKey : null;


    private static string BuildFullName(Member member)
        => string.Join(" ", new[] { member.FirstName, member.MiddleName, member.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    private static string BuildInitials(Member member)
        => string.Concat(new[] { member.FirstName, member.MiddleName, member.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => char.ToUpperInvariant(part![0])));

    private static string? ResolveLeadershipScopeName(
        Leadership leadership,
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

    private static string ResolveProbeTitle(string probeId, GroupedProbeResponse? probe)
        => string.IsNullOrWhiteSpace(probe?.Title) ? probeId : probe.Title;

    private static string ResolveProbePointLabel(string pointId, GroupedProbeResponse? probe)
    {
        if (probe?.Sections is null)
        {
            return pointId;
        }

        foreach (var section in probe.Sections)
        {
            var points = section.Points ?? [];
            var pointIndex = points
                .Select((point, index) => new { point, index })
                .FirstOrDefault(item => item.point.Id == pointId)
                ?.index ?? -1;
            if (pointIndex < 0)
            {
                continue;
            }

            var point = points[pointIndex];
            var pointCode = !string.IsNullOrWhiteSpace(section.Code)
                ? $"{section.Code}{pointIndex + 1}"
                : pointId;
            return string.IsNullOrWhiteSpace(point.Title)
                ? $"точка {pointCode}"
                : $"точка {pointCode} - {point.Title}";
        }

        return pointId;
    }
}
