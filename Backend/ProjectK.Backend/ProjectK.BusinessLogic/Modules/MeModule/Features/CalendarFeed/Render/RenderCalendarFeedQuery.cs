using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Modules.InfrastructureModule.Notifications;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Modules.MeModule.Models;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;
using ProjectK.Common.Security;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.CalendarFeed.Render;

/// <summary>
/// The iCalendar file behind a calendar link, fetched anonymously by Google, Apple or Outlook.
/// <para>
/// It carries what is the person's own across every kurin they stand in: events addressed to them,
/// their гурток or their провід, and what they raised — for a Звʼязковий too, who sees the whole
/// kurin on the web calendar but would drown in it here. Other гуртки's сходини and dated tasks are
/// options of the link.
/// </para>
/// <para>
/// A revoked link, or one of an account that can no longer sign in, answers with a notice instead
/// of the events. A calendar app shows an empty or failing subscription as nothing at all, so this is
/// the only way the person learns, in the place they look, that the link has to be replaced.
/// </para>
/// </summary>
public sealed record RenderCalendarFeedQuery(string Token) : IRequest<ServiceResult<CalendarFeedFile>>;

public sealed class RenderCalendarFeedQueryHandler : IRequestHandler<RenderCalendarFeedQuery, ServiceResult<CalendarFeedFile>>
{
    public const string CalendarName = "Лілейка";
    private const string UidDomain = "lileyka.app";
    private const int MaxTokenLength = 128;
    private const int DefaultDurationMinutes = 60;
    private static readonly TimeSpan PastWindow = TimeSpan.FromDays(90);
    private static readonly TimeSpan FutureWindow = TimeSpan.FromDays(365);
    /// <summary>How long a revoked link keeps showing its notice; matches the cleanup of revoked links.</summary>
    private static readonly TimeSpan NoticeWindow = TimeSpan.FromDays(90);

    private readonly ICalendarFeedStore _feeds;
    private readonly UserManager<AppUser> _userManager;
    private readonly MeAgendaScopes _scopes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly IKraiProfileProvider _profiles;
    private readonly TimeProvider _time;
    private readonly string _siteUrl;

    public RenderCalendarFeedQueryHandler(
        ICalendarFeedStore feeds,
        UserManager<AppUser> userManager,
        MeAgendaScopes scopes,
        IUnitOfWork unitOfWork,
        IMemberDirectory members,
        IKraiProfileProvider profiles,
        TimeProvider time,
        IOptions<EmailSettings> site)
    {
        _feeds = feeds;
        _userManager = userManager;
        _scopes = scopes;
        _unitOfWork = unitOfWork;
        _members = members;
        _profiles = profiles;
        _time = time;
        _siteUrl = site.Value.BaseUrl.TrimEnd('/');
    }

    public async Task<ServiceResult<CalendarFeedFile>> Handle(RenderCalendarFeedQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > MaxTokenLength)
        {
            return NotFound();
        }

        var feed = await _feeds.FindByTokenHashAsync(SecretHash.Sha256Hex(request.Token), cancellationToken);
        if (feed is null)
        {
            return NotFound();
        }

        var now = _time.GetUtcNow().UtcDateTime;
        if (feed.RevokedAtUtc is { } revokedAt)
        {
            return File(IcsWriter.Write(CalendarName, null, Notice(feed.CalendarFeedKey, revokedAt, feed.RevokedReason, now)));
        }

        var user = await _userManager.FindByIdAsync(feed.UserId.ToString());
        if (user is null)
        {
            return NotFound();
        }

        if (!user.CanSignIn())
        {
            return File(IcsWriter.Write(CalendarName, null, Notice(feed.CalendarFeedKey, now, CalendarFeedRevokeReason.Suspended, now)));
        }

        await _feeds.MarkFetchedAsync(feed.CalendarFeedKey, cancellationToken);

        var events = new List<IcsEvent>();
        string? zoneId = null;
        var contexts = await _scopes.BuildForAsync(feed.UserId, cancellationToken);

        foreach (var context in contexts)
        {
            var zone = await _profiles.ForKurinAsync(context.Membership.KurinKey, cancellationToken);
            zoneId ??= zone.Id;
            events.AddRange(await EventsOfAsync(context, feed, zone, now, contexts.Count > 1, cancellationToken));
        }

        return File(IcsWriter.Write(CalendarName, zoneId, events.OrderBy(e => e.StartUtc).ThenBy(e => e.Uid, StringComparer.Ordinal)));
    }

    private async Task<IReadOnlyList<IcsEvent>> EventsOfAsync(
        MeKurinContext context,
        ProjectK.Common.Entities.AuthModule.CalendarFeed feed,
        TimeZoneInfo zone,
        DateTime now,
        bool severalKurins,
        CancellationToken cancellationToken)
    {
        var kurinKey = context.Membership.KurinKey;
        var viewer = context.Viewer;
        var scope = viewer.ToScope();
        var from = now - PastWindow;
        var to = now + FutureWindow;

        var items = (await _unitOfWork.AgendaItems.GetForViewerAsync(
                scope, from, to, onlyDated: true, kind: feed.IncludeTasks ? null : AgendaItemKind.Event,
                cancellationToken, includeKurinSchedules: feed.IncludeSchedules))
            .Where(item => IsOwn(item, scope, viewer) || feed.IncludeSchedules)
            .ToList();
        if (items.Count == 0)
        {
            return [];
        }

        var categories = (await _unitOfWork.AgendaCategories.GetForKurinAsync(kurinKey, includeArchived: true, cancellationToken))
            .ToDictionary(c => c.AgendaCategoryKey);
        var groupNames = feed.IncludeSchedules
            ? (await _unitOfWork.Groups.GetAllAsync(kurinKey, cancellationToken)).ToDictionary(g => g.GroupKey, g => g.Name)
            : new Dictionary<Guid, string>();
        var tasks = items.Where(i => i.Kind == AgendaItemKind.Task).ToList();
        var roster = tasks.Count > 0
            ? await AgendaRoster.LoadAsync(_unitOfWork, _members, kurinKey, tasks, cancellationToken)
            : AgendaRoster.Empty;
        var answers = (await _unitOfWork.AgendaResponses.GetForUserAsync(
                feed.UserId, items.Where(i => i.Kind == AgendaItemKind.Event).Select(i => i.AgendaItemKey).ToList(), from, to, cancellationToken))
            .ToDictionary(r => (r.AgendaItemKey, r.OccurrenceStartUtc), r => r.Status);
        var kurinLine = severalKurins && context.Membership.KurinNumber is { } number ? $"Курінь {number}" : null;

        var rows = new List<IcsEvent>();
        foreach (var item in items)
        {
            var category = item.AgendaCategoryKey is { } key ? categories.GetValueOrDefault(key) : null;
            var own = IsOwn(item, scope, viewer);

            // Only what the toggle adds may be someone else's: a schedule event. Anything else a
            // whole-kurin viewer can merely see stays out of their personal calendar.
            if (!own && category?.IsKurinSchedule != true)
            {
                continue;
            }

            if (item.Kind == AgendaItemKind.Task)
            {
                if (TaskRow(item, viewer, roster, category, kurinLine) is { } task)
                {
                    rows.Add(task);
                }
                continue;
            }

            var summary = own ? item.Title : ScheduleTitle(item, groupNames);
            foreach (var occurrence in AgendaRecurrence.Expand(item, from, to, zone))
            {
                var occurrenceKey = AgendaOccurrences.KeyOf(item, occurrence.StartUtc);
                AgendaRsvpStatus? answer = answers.TryGetValue((item.AgendaItemKey, occurrenceKey), out var status) ? status : null;
                rows.Add(new IcsEvent(
                    Uid: occurrenceKey is { } at ? $"{item.AgendaItemKey:N}-{at:yyyyMMdd'T'HHmmss'Z'}@{UidDomain}" : $"{item.AgendaItemKey:N}@{UidDomain}",
                    StartUtc: occurrence.StartUtc,
                    EndUtc: EndOf(item, occurrence, category),
                    IsAllDay: item.IsAllDay,
                    Summary: summary,
                    Description: Describe(item, kurinKey, answer, kurinLine),
                    Location: item.Location,
                    Category: category?.Name,
                    StampUtc: item.UpdatedDate));
            }
        }

        return rows;
    }

    private static bool IsOwn(AgendaItem item, AgendaViewerScope scope, AgendaViewerContext viewer) =>
        AgendaVisibility.IsAddressedTo(item, scope) || item.CreatedByUserKey == viewer.ViewerUserKey;

    /// <summary>A dated task the person still has to do, as an all-day event on its due day.</summary>
    private IcsEvent? TaskRow(AgendaItem item, AgendaViewerContext viewer, AgendaRoster roster, AgendaCategory? category, string? kurinLine)
    {
        if (AgendaCompletion.StakeOf(item, viewer) is null
            || AgendaCompletion.ViewerStatus(item, viewer, roster) == AgendaItemStatus.Done
            || (item.EndUtc ?? item.StartUtc) is not { } due)
        {
            return null;
        }

        return new IcsEvent(
            Uid: $"{item.AgendaItemKey:N}@{UidDomain}",
            StartUtc: due.Date,
            EndUtc: due.Date,
            IsAllDay: true,
            Summary: $"Задача: {item.Title}",
            Description: Describe(item, item.KurinKey, null, kurinLine),
            Location: item.Location,
            Category: category?.Name,
            StampUtc: item.UpdatedDate);
    }

    /// <summary>The same naming as the web calendar: six «Сходини» on one day say nothing.</summary>
    private static string ScheduleTitle(AgendaItem item, IReadOnlyDictionary<Guid, string> groupNames)
    {
        var groups = item.Assignments
            .Where(a => a.TargetType == AgendaTargetType.Group)
            .Select(a => groupNames.GetValueOrDefault(a.TargetKey))
            .OfType<string>()
            .ToList();
        if (groups.Count == 0 || item.Title.Contains(groups[0], StringComparison.Ordinal))
        {
            return item.Title;
        }

        var more = groups.Count > 1 ? $" +{groups.Count - 1}" : string.Empty;
        return $"{groups[0]}{more} · {item.Title}";
    }

    private static DateTime EndOf(AgendaItem item, AgendaOccurrence occurrence, AgendaCategory? category)
    {
        if (item.IsAllDay)
        {
            return occurrence.EndUtc ?? occurrence.StartUtc;
        }

        return occurrence.EndUtc is { } end && end > occurrence.StartUtc
            ? end
            : occurrence.StartUtc.AddMinutes(category?.DefaultDurationMinutes ?? DefaultDurationMinutes);
    }

    private string Describe(AgendaItem item, Guid kurinKey, AgendaRsvpStatus? answer, string? kurinLine)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            lines.Add(item.Description.Trim());
            lines.Add(string.Empty);
        }
        if (answer is { } given)
        {
            lines.Add($"Твоя відповідь: {AnswerLabel(given)}");
        }
        if (kurinLine is not null)
        {
            lines.Add(kurinLine);
        }
        lines.Add($"Відкрити в Лілейці: {_siteUrl}{AgendaNotificationText.RouteFor(item.Kind, kurinKey)}");
        return string.Join('\n', lines);
    }

    private static string AnswerLabel(AgendaRsvpStatus status) => status switch
    {
        AgendaRsvpStatus.Going => "Іду",
        AgendaRsvpStatus.Maybe => "Можливо",
        _ => "Не йду"
    };

    /// <summary>
    /// One all-day notice a week from the day the link went off, for as long as the link is kept: one
    /// is easy to miss in a busy week, and the notice must still be there when the person next looks.
    /// </summary>
    private IEnumerable<IcsEvent> Notice(Guid feedKey, DateTime offSinceUtc, CalendarFeedRevokeReason? reason, DateTime now)
    {
        var first = offSinceUtc.Date;
        var last = first + NoticeWindow;
        var description =
            $"Посилання, за яким цей календар брав події з Лілейки, вимкнено: {ReasonText(reason)}. Нові події сюди більше не приходять.\n\n" +
            $"Створи нове посилання в Лілейці (Налаштування → Календар у Google, Apple, Outlook), додай його в календар, а цю підписку видали.\n\n" +
            $"{_siteUrl}{RefreshTokenInvalidation.CalendarRoute}";

        for (var day = first; day <= last; day = day.AddDays(7))
        {
            if (day < now.Date.AddDays(-7))
            {
                continue;
            }

            yield return new IcsEvent(
                Uid: $"calendar-off-{feedKey:N}-{day:yyyyMMdd}@{UidDomain}",
                StartUtc: day,
                EndUtc: day,
                IsAllDay: true,
                Summary: "Посилання на календар Лілейки вимкнено",
                Description: description,
                Location: null,
                Category: null,
                StampUtc: offSinceUtc);
        }
    }

    private static string ReasonText(CalendarFeedRevokeReason? reason) => reason switch
    {
        CalendarFeedRevokeReason.Reissued => "його замінило нове посилання",
        CalendarFeedRevokeReason.Disabled => "його вимкнули в налаштуваннях",
        CalendarFeedRevokeReason.Suspended => "акаунт призупинено",
        _ => "після зміни пароля, пошти чи двофакторного входу"
    };

    private static ServiceResult<CalendarFeedFile> File(string content) =>
        new(ResultType.Success, new CalendarFeedFile(content));

    private static ServiceResult<CalendarFeedFile> NotFound() =>
        ServiceResult<CalendarFeedFile>.Failure(ResultType.NotFound, "CalendarFeedNotFound", "No such calendar link.");
}
