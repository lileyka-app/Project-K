using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Records.ProbesAndBadgesModule;
using ProjectK.Common.Rules.KurinModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// Gathers the звіт куреня: the kurin, its гуртки and each person with their way, progress and roles,
/// split into кадра and youth. Who stands where comes from <see cref="KurinReportRosterRules"/>, the
/// header from <see cref="KurinReportHeaderReader"/>, the tags from <see cref="KurinReportTagReader"/>.
/// </summary>
public sealed class KurinReportDataService
{
    private readonly IKurinReportRepository _source;
    private readonly ICurrentUserContext _currentUser;
    private readonly IBlobReadLinks _links;
    private readonly IKurinReportMedia _media;
    private readonly IProbesCatalogReader _probesCatalogReader;
    private readonly IBadgesCatalogReader _badgesCatalogReader;
    // What people have earned belongs to another module; the report asks for it, all at once.
    private readonly IMemberProgressDirectory _progress;
    private readonly IHonoursCatalog _honours;
    private readonly KurinReportHeaderReader _header;
    private readonly KurinReportTagReader _tags;

    public KurinReportDataService(
        ICurrentUserContext currentUser,
        IBlobReadLinks links,
        IKurinReportMedia media,
        IProbesCatalogReader probesCatalogReader,
        IBadgesCatalogReader badgesCatalogReader,
        IMemberProgressDirectory progress,
        IUnitOfWork unitOfWork,
        IHonoursCatalog honours,
        KurinReportHeaderReader header,
        KurinReportTagReader tags)
    {
        _honours = honours;
        _source = unitOfWork.KurinReports;
        _currentUser = currentUser;
        _links = links;
        _media = media;
        _probesCatalogReader = probesCatalogReader;
        _badgesCatalogReader = badgesCatalogReader;
        _progress = progress;
        _header = header;
        _tags = tags;
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
        var mentoredGroupsByUserKey = KurinReportRosterRules.MentoredGroupsByUser(mentorAssignments, groupNamesByKey);
        var tags = await _tags.ReadAsync(kurinKey, source, cancellationToken);

        // A senior kurin's own names for its roles, as its screens show them: its Писар is its «Скриптор» here too.
        var roleNames = KurinRoleNames.Read(kurin.RoleNames);
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
                tags.Of(member),
                roleNames,
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
                KurinReportRosterRules.MentorNames(group.GroupKey, mentorAssignments, memberByUserKey, usersByKey),
                members
                    .Where(member => KurinReportRosterRules.GroupOf(source, member.MemberKey) == group.GroupKey)
                    .OrderBy(member => member.LastName)
                    .ThenBy(member => member.FirstName)
                    .Select(member => new KurinReportGroupMember(
                        member.MemberKey,
                        PersonNames.Full(member.FirstName, member.MiddleName, member.LastName),
                        member.Email,
                        member.PhoneNumber,
                        KurinReportRosterRules.LatestLevelOf(member)))
                    .ToArray()));
        }

        var staffKeys = KurinReportRosterRules.StaffKeys(members, mentorAssignments, groups, kurinKey);

        var staff = reportMembers
            .Where(member => staffKeys.Contains(member.MemberKey))
            .OrderBy(member => member.FullName, StringComparer.CurrentCulture)
            .ToArray();

        var youth = reportMembers
            .Where(member => !staffKeys.Contains(member.MemberKey))
            .OrderBy(member => member.FullName, StringComparer.CurrentCulture)
            .ToArray();

        return new KurinReportData(
            _header.Read(usersByKey),
            new KurinReportKurin(
                kurin.KurinKey,
                kurin.Number,
                kurin.Stanytsia,
                kurin.RegionOrCountry,
                kurin.NamedAfter,
                kurin.Description,
                KurinBranchSetRules.IsSenior(kurin.Branch) ? KurinBranchNames.DescribeSenior(kurin.Numbering) : null),
            reportGroups,
            staff,
            youth,
            KurinReportRosterRules.LevelTally(kurin.Branch, youth),
            reportMembers,
            tags.Shown);
    }

    private async Task<KurinReportMember> BuildMemberReportAsync(
        KurinReportSourceMember member,
        KurinReportSourceData source,
        MemberProgressDetail progress,
        Guid kurinKey,
        IReadOnlyDictionary<Guid, string> groupNamesByKey,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> mentoredGroupsByUserKey,
        IReadOnlyList<string> tagNames,
        IReadOnlyDictionary<LeadershipRole, string> roleNames,
        CancellationToken cancellationToken)
    {
        return new KurinReportMember(
            member.MemberKey,
            member.UserKey,
            KurinReportRosterRules.GroupOf(source, member.MemberKey),
            KurinReportRosterRules.GroupOf(source, member.MemberKey) is Guid groupKey && groupNamesByKey.TryGetValue(groupKey, out var groupName)
                ? groupName
                : null,
            PersonNames.Full(member.FirstName, member.MiddleName, member.LastName),
            KurinReportRosterRules.Initials(member),
            member.Email,
            member.PhoneNumber,
            member.DateOfBirth,
            member.Address,
            member.School,
            _links.For(member.ProfilePhotoBlobName),
            await _media.TryDownloadAsync(member.ProfilePhotoBlobName, cancellationToken),
            KurinReportRosterRules.LatestLevelOf(member),
            member.UserKey is Guid mentorUserKey
                && mentoredGroupsByUserKey.TryGetValue(mentorUserKey, out var mentoredGroups)
                ? mentoredGroups
                : [],
            member.Levels
                .OrderByDescending(item => item.DateAchieved)
                .Select(item => new KurinReportPlastLevel(item.PlastLevel, item.DateAchieved))
                .ToArray(),
            progress.Probes
                .OrderBy(item => item.ProbeId)
                .Select(item =>
                {
                    var probe = _probesCatalogReader.GetGroupedProbeById(item.ProbeId);
                    return new KurinReportProbe(
                        item.ProbeId,
                        KurinReportProgressNames.ProbeTitle(item.ProbeId, probe),
                        item.Status,
                        KurinReportNames.ProbeStatus(item.Status),
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
                    var probe = _probesCatalogReader.GetGroupedProbeById(item.ProbeId);
                    return new KurinReportProbePoint(
                        item.ProbeId,
                        KurinReportProgressNames.ProbeTitle(item.ProbeId, probe),
                        item.PointId,
                        KurinReportProgressNames.ProbePointLabel(item.PointId, probe),
                        item.SignedAtUtc,
                        item.SignedByName,
                        item.SignedByRole);
                })
                .ToArray(),
            progress.ConfirmedBadges
                .OrderBy(item => item.BadgeId)
                .Select(item =>
                {
                    var badge = _badgesCatalogReader.GetBadgeById(item.BadgeId);
                    return new KurinReportBadge(
                        item.BadgeId,
                        badge?.Title ?? item.BadgeId,
                        item.Status,
                        KurinReportNames.BadgeStatus(item.Status),
                        item.ReviewedAtUtc,
                        item.ReviewedByName,
                        item.ReviewedByRole);
                })
                .ToArray(),
            member.Warnings
                .Where(item => item.RevokedAtUtc == null && item.ExpiresAtUtc >= DateTime.UtcNow)
                .OrderByDescending(item => item.IssuedAtUtc)
                .Select(item => new KurinReportWarning(
                    item.Level,
                    KurinReportNames.WarningLevel(item.Level),
                    item.IssuedAtUtc,
                    item.ExpiresAtUtc,
                    item.RevokedAtUtc.HasValue))
                .ToArray(),
            member.Awards
                .OrderByDescending(item => item.DateAcquired)
                .Select(item => new KurinReportAward(
                    item.Level,
                    KurinReportNames.Award(item.Level, item.CatalogId, id => _honours.GetById(id)?.Title),
                    item.DateAcquired,
                    item.Note,
                    item.Status,
                    KurinReportNames.BadgeStatus(item.Status)))
                .ToArray(),
            member.Offices
                .Where(item => KurinReportRosterRules.BelongsToKurin(item, kurinKey, groupNamesByKey))
                .OrderByDescending(item => item.StartDate)
                .Select(item => new KurinReportLeadershipHistory(
                    item.Type,
                    KurinReportNames.LeadershipType(item.Type),
                    item.Role,
                    roleNames.GetValueOrDefault(item.Role) ?? KurinReportNames.LeadershipRole(item.Role),
                    KurinReportRosterRules.LeadershipScopeName(item, groupNamesByKey),
                    item.StartDate,
                    item.EndDate))
                .ToArray(),
            tagNames);
    }
}
