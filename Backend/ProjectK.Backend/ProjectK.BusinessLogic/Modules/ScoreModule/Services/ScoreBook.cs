using ProjectK.BusinessLogic.Modules.ScoreModule.Models;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Interfaces.Modules.ScoreModule;
using ProjectK.Common.Models.Dtos.ScoreModule.Requests;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Score;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Services;

/// <summary>
/// A kurin's точкування read whole: its rows, its youths with their names and гуртки, and the ledger
/// built from them. Every read — the table, a гурток's page, a person's tile — opens the book and
/// takes what it needs, so they never disagree about a number.
/// </summary>
public sealed class ScoreBook
{
    private readonly IReadOnlyDictionary<Guid, MemberSummary> _byMember;
    private readonly IReadOnlyDictionary<Guid, string> _byAccount;

    private ScoreBook(
        Guid kurinKey,
        DateOnly today,
        ScoreAlgorithm algorithm,
        ScoreLedger ledger,
        IReadOnlyDictionary<Guid, KurinMembershipRecord> memberships,
        IReadOnlyDictionary<Guid, MemberSummary> byMember,
        IReadOnlyDictionary<Guid, string> byAccount,
        IReadOnlyDictionary<Guid, string> groupNames,
        IReadOnlyList<ScoreRule> rules,
        IReadOnlyList<ScoreAttendanceRate> attendanceRates,
        IReadOnlyList<ScoreItem> items,
        IReadOnlyList<ScoreStage> stages,
        IReadOnlyList<ScoreEntry> entries,
        IReadOnlyList<ScoreAttendance> attendances,
        DateOnly firstDay)
    {
        KurinKey = kurinKey;
        Today = today;
        Algorithm = algorithm;
        Ledger = ledger;
        Memberships = memberships;
        _byMember = byMember;
        _byAccount = byAccount;
        GroupNames = groupNames;
        Rules = rules;
        AttendanceRates = attendanceRates;
        Items = items;
        Stages = stages;
        Entries = entries;
        Attendances = attendances;
        FirstDay = firstDay;
    }

    public Guid KurinKey { get; }
    public DateOnly Today { get; }
    public ScoreAlgorithm Algorithm { get; }
    public ScoreLedger Ledger { get; }

    /// <summary>Every membership the kurin has had, youths and not, by key.</summary>
    public IReadOnlyDictionary<Guid, KurinMembershipRecord> Memberships { get; }

    public IReadOnlyDictionary<Guid, string> GroupNames { get; }
    public IReadOnlyList<ScoreRule> Rules { get; }
    public IReadOnlyList<ScoreAttendanceRate> AttendanceRates { get; }
    public IReadOnlyList<ScoreItem> Items { get; }
    public IReadOnlyList<ScoreStage> Stages { get; }
    public IReadOnlyList<ScoreEntry> Entries { get; }
    public IReadOnlyList<ScoreAttendance> Attendances { get; }

    /// <summary>The earliest day anything was scored on, or today.</summary>
    public DateOnly FirstDay { get; }

    public static async Task<ScoreBook> OpenAsync(
        Guid kurinKey,
        IScoreUnitOfWork score,
        IUnitOfWork unitOfWork,
        IMembershipDirectory memberships,
        IMemberDirectory members,
        IScoreFactSource facts,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var settings = await score.KurinScoreSettings.GetForKurinAsync(kurinKey, cancellationToken);
        var rules = await score.ScoreRules.GetForKurinAsync(kurinKey, cancellationToken);
        var rates = await score.ScoreAttendanceRates.GetForKurinAsync(kurinKey, cancellationToken);
        var items = await score.ScoreItems.GetForKurinAsync(kurinKey, cancellationToken);
        var stages = await score.ScoreStages.GetForKurinAsync(kurinKey, cancellationToken);
        var moves = await score.ScoreGroupMoves.GetForKurinAsync(kurinKey, cancellationToken);
        var attendances = await score.ScoreAttendances.GetForKurinAsync(kurinKey, cancellationToken);
        var entries = await score.ScoreEntries.GetForKurinAsync(kurinKey, cancellationToken);
        var kurinFacts = await facts.ForKurinAsync(kurinKey, cancellationToken);

        var inKurin = (await memberships.GetInKurinAsync(kurinKey, cancellationToken)).ToDictionary(m => m.MembershipKey);
        var people = await members.GetByKurinAsync(kurinKey, cancellationToken);
        var groupNames = (await unitOfWork.Groups.GetAllAsync(kurinKey, cancellationToken)).ToDictionary(g => g.GroupKey, g => g.Name);

        // The ledger prices a mark by its event's group, so every marked event's group is read at once.
        var eventKeys = attendances.Select(a => a.AgendaItemKey).Distinct().ToList();
        var eventCategories = await unitOfWork.AgendaItems.GetCategoryKeysAsync(eventKeys, cancellationToken);

        var youths = inKurin.Values
            .Where(m => m.Kind == MembershipKind.Youth)
            // A youth who left keeps the гурток they left from: what they earned before leaving, and
            // the days they were there, still count for it. LeftOn bounds the days.
            .Select(m => new ScoreMember(
                m.MembershipKey,
                m.GroupKey,
                DateOnly.FromDateTime(m.JoinedAtUtc),
                m.LeftAtUtc is { } left ? DateOnly.FromDateTime(left) : null))
            .ToList();

        var algorithm = settings?.Algorithm ?? ScoreAlgorithm.Average;
        var ledger = new ScoreLedger(algorithm, today, youths, moves, rules, rates, eventCategories, attendances, entries, kurinFacts);

        var firstDay = attendances.Select(a => DateOnly.FromDateTime(a.OccurrenceStartUtc))
            .Concat(entries.Select(e => e.OccurredOn))
            .Concat(kurinFacts.Select(f => f.On))
            .DefaultIfEmpty(today)
            .Min();

        return new ScoreBook(
            kurinKey, today, algorithm, ledger, inKurin,
            people.ToDictionary(p => p.MemberKey),
            people.Where(p => p.UserKey.HasValue).GroupBy(p => p.UserKey!.Value).ToDictionary(g => g.Key, g => g.First().FullName),
            groupNames, rules, rates, items, stages, entries, attendances, firstDay);
    }

    /// <summary>The days a request asks about; null when it names a stage that is not here.</summary>
    public (ScorePeriod Period, ScorePeriodDto Dto)? ResolvePeriod(ScorePeriodQuery query)
    {
        if (query.StageKey is { } stageKey)
        {
            var stage = Stages.FirstOrDefault(s => s.ScoreStageKey == stageKey);
            return stage is null ? null : (new ScorePeriod(stage.FromDate, stage.ToDate), StageDto(stage));
        }

        var year = query.Year ?? PlastYear.Of(Today);
        return (new ScorePeriod(PlastYear.FirstDay(year), PlastYear.LastDay(year)), YearDto(year));
    }

    /// <summary>Every пластовий рік from the first scored day to today, newest first, then the stages.</summary>
    public ScorePeriodsDto Periods()
    {
        var first = PlastYear.Of(FirstDay);
        var current = PlastYear.Of(Today);
        var years = Enumerable.Range(first, current - first + 1).Reverse().Select(YearDto).ToList();
        return new ScorePeriodsDto { Years = years, Stages = Stages.OrderByDescending(s => s.FromDate).Select(StageDto).ToList() };
    }

    private static ScorePeriodDto YearDto(int year) => new()
    {
        Kind = "Year",
        Year = year,
        StageKey = null,
        Label = PlastYear.Label(year),
        From = PlastYear.FirstDay(year),
        To = PlastYear.LastDay(year)
    };

    private static ScorePeriodDto StageDto(ScoreStage stage) => new()
    {
        Kind = "Stage",
        Year = null,
        StageKey = stage.ScoreStageKey,
        Label = stage.Name,
        From = stage.FromDate,
        To = stage.ToDate
    };

    public string NameOfMembership(Guid membershipKey) =>
        Memberships.TryGetValue(membershipKey, out var m) && _byMember.TryGetValue(m.MemberKey, out var p) ? p.FullName : "—";

    public Guid? MemberKeyOf(Guid membershipKey) =>
        Memberships.TryGetValue(membershipKey, out var m) ? m.MemberKey : null;

    public string? NameOfAccount(Guid? userKey) => userKey is { } key ? _byAccount.GetValueOrDefault(key) : null;

    public string GroupName(Guid? groupKey) => groupKey is { } key ? GroupNames.GetValueOrDefault(key, "—") : "—";

    public MemberSummary? Person(Guid memberKey) => _byMember.GetValueOrDefault(memberKey);

    /// <summary>Everyone currently in the kurin's youth body, with the account they answer invitations from.</summary>
    public IEnumerable<(KurinMembershipRecord Membership, MemberSummary Person)> CurrentYouths() =>
        Memberships.Values
            .Where(m => m.Kind == MembershipKind.Youth && m.LeftAtUtc is null)
            .Select(m => (m, _byMember.GetValueOrDefault(m.MemberKey)))
            .Where(x => x.Item2 is not null)
            .Select(x => (x.m, x.Item2!));

    public ScoreEntryDto ToDto(ScoreEntry e) => new()
    {
        ScoreEntryKey = e.ScoreEntryKey,
        MembershipKey = e.MembershipKey,
        MemberKey = e.MembershipKey is { } mk ? MemberKeyOf(mk) : null,
        MemberName = e.MembershipKey is { } mk2 ? NameOfMembership(mk2) : null,
        GroupKey = e.GroupKey ?? (e.MembershipKey is { } mk3 ? Ledger.GroupOn(mk3, e.OccurredOn) : null),
        GroupName = GroupName(e.GroupKey ?? (e.MembershipKey is { } mk4 ? Ledger.GroupOn(mk4, e.OccurredOn) : null)),
        IsForGroup = e.GroupKey.HasValue,
        ScoreItemKey = e.ScoreItemKey,
        ItemName = e.ScoreItemKey is { } ik ? Items.FirstOrDefault(i => i.ScoreItemKey == ik)?.Name : null,
        Points = e.Points,
        Reason = e.Reason,
        AgendaItemKey = e.AgendaItemKey,
        OccurrenceStartUtc = e.OccurrenceStartUtc is { } o ? DateTime.SpecifyKind(o, DateTimeKind.Utc) : null,
        OccurredOn = e.OccurredOn,
        CreatedByName = NameOfAccount(e.CreatedByUserKey),
        CreatedAtUtc = DateTime.SpecifyKind(e.CreatedDate, DateTimeKind.Utc)
    };

    public static ScoreItemDto ToDto(ScoreItem i) => new()
    {
        ScoreItemKey = i.ScoreItemKey,
        Name = i.Name,
        Points = i.Points,
        IsArchived = i.IsArchived
    };

    /// <inheritdoc cref="ScoreAttendanceBook.PointsFor"/>
    public int AttendancePoints(Guid agendaItemKey, Guid? categoryKey) =>
        ScoreAttendanceBook.PointsFor(AttendanceRates, agendaItemKey, categoryKey);
}
