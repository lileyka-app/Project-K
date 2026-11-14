using ProjectK.BusinessLogic.Modules.KurinModule;
using System.Globalization;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Reports;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Life;

/// <summary>The caller's own life story as a PDF. <paramref name="TimeZone"/> is the reader's IANA zone.</summary>
public sealed record ExportMyLifePdfQuery(string? TimeZone) : IRequest<ServiceResult<LifeStoryPdf>>;

/// <summary>
/// Reads the person's dossier — memberships, offices, ступені, відзначення, проби and вмілості — and
/// what they wrote themself, and lays it out oldest first. The dossier is the same one a card reads;
/// for its own owner it hides nothing.
/// </summary>
public sealed class ExportMyLifePdfQueryHandler : IRequestHandler<ExportMyLifePdfQuery, ServiceResult<LifeStoryPdf>>
{
    private static readonly IReadOnlyCollection<string> Folders =
    [
        MemberDossierFolders.Memberships, MemberDossierFolders.Offices, MemberDossierFolders.Levels,
        MemberDossierFolders.Awards, MemberDossierFolders.Progress
    ];

    private readonly LifeStoryOwner _owner;
    private readonly ISender _sender;
    private readonly IMemberUnitOfWork _members;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHonoursCatalog _honours;
    private readonly IProbesCatalogService _probes;
    private readonly IBadgesCatalogService _badges;
    private readonly ILifeStoryPdfRenderer _renderer;
    private readonly TimeProvider _time;

    public ExportMyLifePdfQueryHandler(
        LifeStoryOwner owner,
        ISender sender,
        IMemberUnitOfWork members,
        IUnitOfWork unitOfWork,
        IHonoursCatalog honours,
        IProbesCatalogService probes,
        IBadgesCatalogService badges,
        ILifeStoryPdfRenderer renderer,
        TimeProvider time)
    {
        _owner = owner;
        _sender = sender;
        _members = members;
        _unitOfWork = unitOfWork;
        _honours = honours;
        _probes = probes;
        _badges = badges;
        _renderer = renderer;
        _time = time;
    }

    public async Task<ServiceResult<LifeStoryPdf>> Handle(ExportMyLifePdfQuery request, CancellationToken cancellationToken)
    {
        if (await _owner.MemberKeyAsync(cancellationToken) is not { } memberKey)
        {
            return ServiceResult<LifeStoryPdf>.Failure(ResultType.Forbidden, MemberErrors.NoOwnCard, "No person stands behind this account.");
        }

        var dossier = await _sender.Send(new GetMemberDossierQuery(memberKey, Folders), cancellationToken);
        if (dossier.Type != ResultType.Success || dossier.Data is not { } box)
        {
            return ServiceResult<LifeStoryPdf>.Failure(
                dossier.Type == ResultType.Success ? ResultType.NotFound : dossier.Type,
                dossier.ErrorCode ?? MemberErrors.MemberNotFound,
                dossier.ErrorMessage ?? "Your record could not be read.");
        }

        var memberships = box.Memberships ?? [];
        var entries = await _members.MemberLifeEntries.GetForMemberAsync(memberKey, cancellationToken);
        var roleNames = await RoleNamesAsync(box.Offices ?? [], cancellationToken);

        var sections = new List<LifeStorySection>
        {
            new("Членства", memberships.OrderBy(m => m.JoinedAtUtc).Select(MembershipRow).ToList()),
            new("Ступені", (box.Levels ?? []).OrderBy(l => l.DateAchieved).Select(LevelRow).ToList()),
            new("Ролі", (box.Offices ?? []).OrderBy(o => o.StartDate).Select(o => OfficeRow(o, memberships, roleNames)).ToList()),
            new("Відзначення", (box.Awards ?? [])
                .Where(a => a.Status == BadgeProgressStatus.Confirmed)
                .OrderBy(a => a.DateAcquired)
                .Select(AwardRow).ToList()),
            new("Проби", (box.Progress?.Probes ?? [])
                .Where(p => p.Status is ProbeProgressStatus.Completed or ProbeProgressStatus.Verified)
                .OrderBy(p => p.VerifiedAtUtc ?? p.CompletedAtUtc)
                .Select(ProbeRow).ToList()),
            new("Вмілості", (box.Progress?.Badges ?? [])
                .Where(b => b.Status == BadgeProgressStatus.Confirmed)
                .OrderBy(b => b.ReviewedAtUtc)
                .Select(BadgeRow).ToList()),
            new("Вишколи", entries.Where(e => e.Kind == LifeEntryKind.Training).Select(EntryRow).ToList()),
            new("Табори", entries.Where(e => e.Kind == LifeEntryKind.Camp).Select(EntryRow).ToList()),
            new("Заходи", entries.Where(e => e.Kind == LifeEntryKind.Event).Select(EntryRow).ToList())
        };

        var profile = box.Profile;
        var fullName = string.Join(' ', new[] { profile.FirstName, profile.MiddleName, profile.LastName }.Where(p => !string.IsNullOrWhiteSpace(p)));
        var now = _time.GetUtcNow();
        var story = new LifeStoryData(fullName, now.UtcDateTime, sections.Where(s => s.Rows.Count > 0).ToList());

        var bytes = _renderer.Render(story, request.TimeZone);
        return new ServiceResult<LifeStoryPdf>(ResultType.Success, new LifeStoryPdf(bytes, $"zhyttiepys-{now:yyyyMMdd}.pdf"));
    }

    /// <summary>Each kurin's own names for its roles, for the kurins the person held one in.</summary>
    private async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<LeadershipRole, string>>> RoleNamesAsync(
        IEnumerable<LeadershipHistoryDto> offices, CancellationToken cancellationToken)
    {
        var names = new Dictionary<Guid, IReadOnlyDictionary<LeadershipRole, string>>();
        foreach (var kurinKey in offices.Select(o => o.KurinKey).OfType<Guid>().Distinct())
        {
            if (await _unitOfWork.Kurins.GetByKeyAsync(kurinKey, cancellationToken) is { } kurin)
            {
                names[kurinKey] = KurinRoleNames.Read(kurin.RoleNames);
            }
        }

        return names;
    }

    private static LifeStoryRow MembershipRow(MembershipRecord membership)
    {
        var detail = new[]
        {
            membership.GroupName is { } group ? $"гурток {group}" : null,
            membership.Kind == MembershipKind.Staff ? "виховний склад" : null
        }.Where(part => part is not null);

        return new LifeStoryRow(
            Period(DateOnly.FromDateTime(membership.JoinedAtUtc), membership.LeftAtUtc is { } left ? DateOnly.FromDateTime(left) : null, open: true),
            KurinName(membership),
            Join(detail));
    }

    private LifeStoryRow LevelRow(PlastLevelHistoryDto level)
    {
        // The day of joining has no ступінь of its own to name; the card calls it the same.
        var title = level.PlastLevel == PlastLevel.Entry ? "Вступ до Пласту"
            : level.CatalogId is { } id && _honours.GetById(id) is { } honour ? honour.Title
            : PlastLevelNames.Of(level.PlastLevel);
        return new LifeStoryRow(Day(level.DateAchieved), title, KurinBranchNames.Label(PlastLadder.BranchOf(level.PlastLevel)));
    }

    private static LifeStoryRow OfficeRow(
        LeadershipHistoryDto office,
        IReadOnlyCollection<MembershipRecord> memberships,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<LeadershipRole, string>> roleNames)
    {
        var role = office.KurinKey is { } key && roleNames.TryGetValue(key, out var names) && names.TryGetValue(office.Role, out var own)
            ? own
            : KurinReportTerminology.LeadershipRole(office.Role);
        var kurin = memberships.FirstOrDefault(m => m.KurinKey == office.KurinKey) is { } membership ? KurinName(membership) : null;
        var where = Join([kurin, office.GroupName is { } group ? $"гурток {group}" : null]);
        return new LifeStoryRow(Period(office.StartDate, office.EndDate, open: true), role, where);
    }

    private LifeStoryRow AwardRow(MemberAwardDto award)
    {
        var title = KurinReportTerminology.Award(award.Level, award.CatalogId, id => _honours.GetById(id)?.Title);
        var line = award.Level is null ? "УСП/УПС" : "УПЮ";
        return new LifeStoryRow(Day(DateOnly.FromDateTime(award.DateAcquired)), title, Join([line, award.Note]));
    }

    private LifeStoryRow ProbeRow(ProbeProgressRecord probe)
    {
        var title = _probes.GetGroupedProbeById(probe.ProbeId)?.Title is { Length: > 0 } named ? named : probe.ProbeId;
        var at = probe.VerifiedAtUtc ?? probe.CompletedAtUtc;
        return new LifeStoryRow(at is { } date ? Day(DateOnly.FromDateTime(date)) : "без дати", title,
            probe.Status == ProbeProgressStatus.Verified ? "підтверджено" : "підписано");
    }

    private LifeStoryRow BadgeRow(BadgeProgressRecord badge)
    {
        var title = _badges.GetBadgeById(badge.BadgeId)?.Title ?? badge.BadgeId;
        return new LifeStoryRow(badge.ReviewedAtUtc is { } date ? Day(DateOnly.FromDateTime(date)) : "без дати", title, null);
    }

    private static LifeStoryRow EntryRow(ProjectK.Common.Entities.KurinModule.MemberLifeEntry entry)
    {
        var what = entry.Kind == LifeEntryKind.Training ? TrainingName(entry.Course, entry.Title, entry.Number) : entry.Title ?? "-";
        var detail = Join([
            entry.Place,
            entry.Commandant is { } commandant ? $"комендант: {commandant}" : null,
            entry.Role
        ]);
        return new LifeStoryRow(Period(entry.StartDate, entry.EndDate, open: false), what, detail);
    }

    /// <summary>«12-й КВЗ», «ЛШ», or the name the person gave a course the list does not know.</summary>
    public static string TrainingName(TrainingCourse? course, string? title, int? number)
    {
        var name = course switch
        {
            TrainingCourse.Kvdch => "КВДЧ",
            TrainingCourse.Kvv => "КВВ",
            TrainingCourse.Rov => "РОВ",
            TrainingCourse.Kvz => "КВЗ",
            TrainingCourse.LisovaShkola => "ЛШ",
            TrainingCourse.ShkolaBulavnykh => "ШБ",
            _ => title ?? "Вишкіл"
        };
        return number is { } n ? $"{n}-й {name}" : name;
    }

    /// <summary>A senior kurin by its name and гілки, an УПЮ one by its number and patron.</summary>
    private static string KurinName(MembershipRecord membership)
    {
        if (KurinBranchSet.IsSenior(membership.Branch))
        {
            var branches = KurinBranchNames.DescribeSenior(membership.Numbering);
            return membership.KurinNamedAfter is { } name ? $"«{name}» · {branches}" : branches;
        }

        var kurin = membership.KurinNumber is { } number ? $"Курінь ч. {number}" : "Підготовчий курінь";
        return membership.KurinNamedAfter is { } patron ? $"{kurin} ім. {patron}" : kurin;
    }

    private static string Day(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>«01.09.2015 – 31.08.2021»; an open stretch runs «– дотепер», a one-day one is just the day.</summary>
    private static string Period(DateOnly from, DateOnly? to, bool open)
        => to is { } end
            ? end == from ? Day(from) : $"{Day(from)} – {Day(end)}"
            : open ? $"{Day(from)} – дотепер" : Day(from);

    private static string? Join(IEnumerable<string?> parts)
    {
        var kept = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        return kept.Count == 0 ? null : string.Join(" · ", kept);
    }
}
