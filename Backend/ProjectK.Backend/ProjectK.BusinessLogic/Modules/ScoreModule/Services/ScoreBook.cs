using ProjectK.Common.Dtos.ScoreModule;
using ProjectK.Common.Dtos.ScoreModule.Requests;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.ScoreModule;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Records.ScoreModule;
using ProjectK.Common.Rules.DuesModule;
using ProjectK.Common.Rules.KurinModule;

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

    internal ScoreBook(
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

    /// <inheritdoc cref="ScoreAttendanceBook.PointsFor"/>
    public int AttendancePoints(Guid agendaItemKey, Guid? categoryKey) =>
        ScoreAttendanceBook.PointsFor(AttendanceRates, agendaItemKey, categoryKey);
}
