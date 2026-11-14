using FluentAssertions;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Score;

namespace ProjectK.BusinessLogic.Tests.ScoreModule;

/// <summary>
/// The rules of точкування, one by one. The period is October 2026:
/// 31 days, and "today" is its last day unless a test says otherwise.
/// </summary>
public class ScoreLedgerTests
{
    private static readonly Guid Kurin = Guid.NewGuid();
    private static readonly Guid Sokoly = Guid.NewGuid();
    private static readonly Guid Levy = Guid.NewGuid();
    private static readonly Guid Skhodyny = Guid.NewGuid();
    private static readonly Guid Tabir = Guid.NewGuid();

    private static readonly ScorePeriod October = new(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

    private readonly List<ScoreMember> _members = [];
    private readonly List<ScoreGroupMove> _moves = [];
    private readonly List<ScoreRule> _rules = [];
    private readonly List<ScoreAttendanceRate> _rates =
    [
        new() { KurinKey = Kurin, AgendaCategoryKey = Skhodyny, Points = 1 },
        new() { KurinKey = Kurin, AgendaCategoryKey = Tabir, Points = 5 }
    ];
    private readonly Dictionary<Guid, Guid?> _events = [];
    private readonly List<ScoreAttendance> _marks = [];
    private readonly List<ScoreEntry> _entries = [];
    private readonly List<ScoreFact> _facts = [];

    private ScoreAlgorithm _algorithm = ScoreAlgorithm.Average;
    private DateOnly _today = new(2026, 10, 31);

    private ScoreLedger Ledger() =>
        new(_algorithm, _today, _members, _moves, _rules, _rates, _events, _marks, _entries, _facts);

    private Guid Youth(Guid? group, DateOnly? joined = null, DateOnly? left = null)
    {
        var key = Guid.NewGuid();
        _members.Add(new ScoreMember(key, group, joined ?? new DateOnly(2025, 9, 1), left));
        return key;
    }

    private Guid Event(Guid? category)
    {
        var key = Guid.NewGuid();
        _events[key] = category;
        return key;
    }

    private void Mark(Guid membership, Guid item, int day) =>
        _marks.Add(new ScoreAttendance
        {
            KurinKey = Kurin,
            MembershipKey = membership,
            AgendaItemKey = item,
            OccurrenceStartUtc = new DateTime(2026, 10, day, 16, 0, 0, DateTimeKind.Utc),
            MarkedAtUtc = DateTime.UtcNow
        });

    private ScoreEntry Give(Guid membership, int points, int day, Guid? item = null)
    {
        var entry = new ScoreEntry
        {
            KurinKey = Kurin,
            MembershipKey = membership,
            ScoreItemKey = item,
            Points = points,
            OccurredOn = new DateOnly(2026, 10, day)
        };
        _entries.Add(entry);
        return entry;
    }

    private void Move(Guid membership, Guid? from, Guid? to, int day) =>
        _moves.Add(new ScoreGroupMove
        {
            KurinKey = Kurin,
            MembershipKey = membership,
            FromGroupKey = from,
            ToGroupKey = to,
            MovedAtUtc = new DateTime(2026, 10, day, 12, 0, 0, DateTimeKind.Utc)
        });

    private ScoreGroupTotal Group(Guid key) => Ledger().Groups(October, [Sokoly, Levy]).Single(g => g.GroupKey == key);

    [Fact]
    public void Attendance_IsWorthWhatItsGroupOfEventsIsWorth()
    {
        var youth = Youth(Sokoly);
        Mark(youth, Event(Skhodyny), 3);
        Mark(youth, Event(Tabir), 10);
        Mark(youth, Event(null), 12);

        Ledger().People(October).Single().Total.Should().Be(6, "сходини 1 + табір 5; an event with no group is worth nothing");
    }

    // Fixing a rate is meant to fix the past: the points are worked out when read, not kept on the mark.
    [Fact]
    public void AnEventsOwnRate_WinsOverItsGroup_AndRewritesMarksAlreadyIn()
    {
        var youth = Youth(Sokoly);
        var vatra = Event(Skhodyny);
        Mark(youth, vatra, 3);

        _rates.Add(new ScoreAttendanceRate { KurinKey = Kurin, AgendaItemKey = vatra, Points = 3 });

        Ledger().People(October).Single().Total.Should().Be(3);
    }

    [Fact]
    public void ARemovedMark_EarnsNothing()
    {
        var youth = Youth(Sokoly);
        Mark(youth, Event(Skhodyny), 3);
        _marks[0].RemovedAtUtc = DateTime.UtcNow;

        Ledger().People(October).Should().BeEmpty();
    }

    [Fact]
    public void Entries_CountAsPositionsOrFree_AndDeletedOnesDoNot()
    {
        var youth = Youth(Sokoly);
        Give(youth, 2, 3, item: Guid.NewGuid());
        Give(youth, -1, 4, item: Guid.NewGuid());
        Give(youth, 3, 5);
        Give(youth, 10, 6).DeletedAtUtc = DateTime.UtcNow;

        var person = Ledger().People(October).Single();

        person.Total.Should().Be(4);
        person.BySource.Should().BeEquivalentTo(new Dictionary<ScoreSource, int> { [ScoreSource.Item] = 1, [ScoreSource.Free] = 3 });
    }

    [Fact]
    public void AnAutomaticSource_IsWorthItsRuleOnTheDay()
    {
        var youth = Youth(Sokoly);
        _rules.Add(new ScoreRule { KurinKey = Kurin, Source = ScoreSource.Skill, FromDate = new DateOnly(2026, 1, 1), Points = 2 });
        _rules.Add(new ScoreRule { KurinKey = Kurin, Source = ScoreSource.Skill, FromDate = new DateOnly(2026, 10, 15), Points = 4 });
        _facts.Add(new ScoreFact(youth, ScoreSource.Skill, 0, new DateOnly(2026, 10, 10)));
        _facts.Add(new ScoreFact(youth, ScoreSource.Skill, 0, new DateOnly(2026, 10, 20)));
        _facts.Add(new ScoreFact(youth, ScoreSource.Probe, 0, new DateOnly(2026, 10, 20)));

        Ledger().People(October).Single().Total.Should().Be(6, "2 before the change, 4 after; a source with no rule is off");
    }

    [Fact]
    public void AWarning_TakesPoints_ByItsLevel()
    {
        var youth = Youth(Sokoly);
        Give(youth, 5, 2);
        _rules.Add(new ScoreRule { KurinKey = Kurin, Source = ScoreSource.Warning, Variant = 1, FromDate = new DateOnly(2026, 1, 1), Points = -2 });
        _rules.Add(new ScoreRule { KurinKey = Kurin, Source = ScoreSource.Warning, Variant = 2, FromDate = new DateOnly(2026, 1, 1), Points = -5 });
        _facts.Add(new ScoreFact(youth, ScoreSource.Warning, 2, new DateOnly(2026, 10, 9)));

        Ledger().People(October).Single().Total.Should().Be(0);
    }

    [Fact]
    public void OnlyThePeriod_Counts()
    {
        var youth = Youth(Sokoly);
        Give(youth, 4, 15);
        _entries.Add(new ScoreEntry { KurinKey = Kurin, MembershipKey = youth, Points = 7, OccurredOn = new DateOnly(2026, 9, 30) });

        Ledger().People(October).Single().Total.Should().Be(4);
    }

    // A point stays with the гурток it was earned in; the person carries all of theirs.
    [Fact]
    public void AfterAMove_PointsStayWithTheGurtokTheyWereEarnedIn()
    {
        var youth = Youth(Levy);
        Move(youth, Sokoly, Levy, 16);
        Give(youth, 3, 10);
        Give(youth, 5, 20);

        var ledger = Ledger();

        ledger.People(October).Single().Total.Should().Be(8);
        ledger.People(October, Sokoly).Single().Total.Should().Be(3);
        ledger.People(October, Levy).Single().Total.Should().Be(5);
        ledger.GroupOn(youth, new DateOnly(2026, 10, 15)).Should().Be(Sokoly);
        ledger.GroupOn(youth, new DateOnly(2026, 10, 16)).Should().Be(Levy, "the day of the move belongs to the new гурток");
    }

    [Fact]
    public void Average_DividesByYouths_SoASmallActiveGurtokLeads()
    {
        var a = Youth(Sokoly);
        var b = Youth(Sokoly);
        Give(a, 10, 3);
        Give(b, 10, 3);
        for (var i = 0; i < 4; i++)
        {
            Give(Youth(Levy), 3, 3);
        }

        var groups = Ledger().Groups(October, [Sokoly, Levy]);

        groups.Select(g => g.GroupKey).Should().Equal(Sokoly, Levy);
        groups[0].Should().BeEquivalentTo(new { YouthPoints = 20, YouthCount = 2m, Average = 10m, Score = 10m, OtherScore = 20m });
        groups[1].Should().BeEquivalentTo(new { YouthPoints = 12, YouthCount = 4m, Average = 3m, Score = 3m, OtherScore = 12m });
    }

    [Fact]
    public void Sum_RanksByAllThePoints_AndShowsTheAverageAlongside()
    {
        _algorithm = ScoreAlgorithm.Sum;
        Give(Youth(Sokoly), 10, 3);
        for (var i = 0; i < 4; i++)
        {
            Give(Youth(Levy), 3, 3);
        }

        var groups = Ledger().Groups(October, [Sokoly, Levy]);

        groups.Select(g => g.GroupKey).Should().Equal(Levy, Sokoly);
        groups[0].Should().BeEquivalentTo(new { Score = 12m, OtherScore = 3m });
    }

    // Someone there for part of the period counts for that part — or leaving a weak youth out just
    // before the end would lift the гурток's average.
    [Fact]
    public void AYouthThereForPartOfThePeriod_CountsForThatPart()
    {
        Youth(Sokoly);
        Youth(Sokoly, left: new DateOnly(2026, 10, 15));
        var mover = Youth(Levy);
        Move(mover, Sokoly, Levy, 16);

        Group(Sokoly).YouthCount.Should().Be(1.97m, "31 + 15 + 15 youth-days over 31 days");
        Group(Levy).YouthCount.Should().Be(0.52m, "16 youth-days over 31 days");
    }

    [Fact]
    public void ThePeriodsDays_StopAtToday()
    {
        _today = new DateOnly(2026, 10, 10);
        Give(Youth(Sokoly), 10, 3);
        Youth(Sokoly, joined: new DateOnly(2026, 10, 6));

        Group(Sokoly).YouthCount.Should().Be(1.5m, "10 + 5 youth-days over the 10 days so far");
    }

    [Fact]
    public void PointsForTheGurtokAsAWhole_AreAddedAfter_AndNotDivided()
    {
        Give(Youth(Sokoly), 4, 3);
        Give(Youth(Sokoly), 2, 3);
        _entries.Add(new ScoreEntry { KurinKey = Kurin, GroupKey = Sokoly, Points = 5, OccurredOn = new DateOnly(2026, 10, 7) });

        Group(Sokoly).Should().BeEquivalentTo(new { YouthPoints = 6, Average = 3m, GroupPoints = 5, Score = 8m, OtherScore = 11m });
    }

    [Fact]
    public void EveryGurtokAskedFor_IsInTheTable_EvenWithNothing()
    {
        var groups = Ledger().Groups(October, [Sokoly, Levy]);

        groups.Should().HaveCount(2);
        groups.Should().OnlyContain(g => g.Score == 0 && g.YouthCount == 0);
    }

    [Fact]
    public void SomeoneWhoIsNotAScoredYouth_EarnsNothing()
    {
        var stranger = Guid.NewGuid();
        Mark(stranger, Event(Skhodyny), 3);
        _entries.Add(new ScoreEntry { KurinKey = Kurin, MembershipKey = stranger, Points = 9, OccurredOn = new DateOnly(2026, 10, 3) });

        Ledger().People(October).Should().BeEmpty();
    }

    [Fact]
    public void AYouthInNoGurtok_KeepsTheirPoints_ButNoGurtokGetsThem()
    {
        var youth = Youth(null);
        Give(youth, 4, 3);

        var ledger = Ledger();

        ledger.People(October).Single().Total.Should().Be(4);
        ledger.Groups(October, [Sokoly]).Single().YouthPoints.Should().Be(0);
    }
}
