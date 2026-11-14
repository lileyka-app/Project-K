using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Enums.ScoreModule;
using ProjectK.Common.Records.ScoreModule;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Services;

/// <summary>
/// Works a kurin's точкування out from its rows: what each youth earned, from where, for which
/// гурток, and how the гуртки stand. Pure, so every points rule lives here and is testable.
/// <list type="bullet">
/// <item>a point belongs to the гурток the youth was in on the day it was earned, and stays there
/// when they move;</item>
/// <item>a mark of attendance is worth what its event is worth now — the event's own rate, else its
/// group's — so fixing a rate fixes the past;</item>
/// <item>an automatic source is worth what its rule was on the day;</item>
/// <item>a гурток is ranked by its youths' points per youth, or by their sum, as the kurin chose;
/// either way, points given to the гурток as a whole are added after and never divided.</item>
/// </list>
/// </summary>
public sealed class ScoreLedger
{
    private readonly ScoreAlgorithm _algorithm;
    private readonly DateOnly _today;
    private readonly IReadOnlyDictionary<Guid, ScoreMember> _members;
    private readonly ILookup<Guid, ScoreGroupMove> _moves;
    private readonly IReadOnlyList<ScoreLine> _lines;
    private readonly IReadOnlyList<ScoreEntry> _groupEntries;

    /// <summary>
    /// Takes the kurin's rows as they are. <paramref name="eventCategories"/> is the group of each event
    /// a mark is for, null for an event with none.
    /// </summary>
    public ScoreLedger(
        ScoreAlgorithm algorithm,
        DateOnly today,
        IEnumerable<ScoreMember> members,
        IEnumerable<ScoreGroupMove> moves,
        IEnumerable<ScoreRule> rules,
        IEnumerable<ScoreAttendanceRate> attendanceRates,
        IReadOnlyDictionary<Guid, Guid?> eventCategories,
        IEnumerable<ScoreAttendance> attendances,
        IEnumerable<ScoreEntry> entries,
        IEnumerable<ScoreFact> facts)
    {
        _algorithm = algorithm;
        _today = today;
        _members = members.ToDictionary(m => m.MembershipKey);
        _moves = moves.OrderBy(m => m.MovedAtUtc).ToLookup(m => m.MembershipKey);

        var ruleList = rules.OrderBy(r => r.FromDate).ToList();
        var rates = attendanceRates.ToList();
        var byItem = rates.Where(r => r.AgendaItemKey.HasValue).ToDictionary(r => r.AgendaItemKey!.Value, r => r.Points);
        var byCategory = rates.Where(r => r.AgendaCategoryKey.HasValue).ToDictionary(r => r.AgendaCategoryKey!.Value, r => r.Points);

        var standing = entries.Where(e => !e.IsDeleted).ToList();
        _groupEntries = standing.Where(e => e.GroupKey.HasValue && e.MembershipKey is null).ToList();

        var lines = new List<ScoreLine>();
        foreach (var mark in attendances.Where(a => !a.IsRemoved && _members.ContainsKey(a.MembershipKey)))
        {
            var points = AttendancePoints(mark.AgendaItemKey, eventCategories, byItem, byCategory);
            Add(lines, mark.MembershipKey, ScoreSource.Attendance, points, DateOnly.FromDateTime(mark.OccurrenceStartUtc));
        }

        foreach (var entry in standing.Where(e => e.MembershipKey.HasValue && _members.ContainsKey(e.MembershipKey.Value)))
        {
            var source = entry.ScoreItemKey.HasValue ? ScoreSource.Item : ScoreSource.Free;
            Add(lines, entry.MembershipKey!.Value, source, entry.Points, entry.OccurredOn);
        }

        foreach (var fact in facts.Where(f => _members.ContainsKey(f.MembershipKey)))
        {
            var rule = ruleList.LastOrDefault(r => r.Source == fact.Source && r.Variant == fact.Variant && r.FromDate <= fact.On);
            Add(lines, fact.MembershipKey, fact.Source, rule?.Points ?? 0, fact.On);
        }

        _lines = lines;
    }

    /// <summary>Every point earned in the period, as it was earned.</summary>
    public IEnumerable<ScoreLine> Lines(ScorePeriod period) => _lines.Where(l => period.Contains(l.On));

    /// <summary>The гурток a youth stood in on a day; null for none.</summary>
    public Guid? GroupOn(Guid membershipKey, DateOnly day)
    {
        if (!_members.TryGetValue(membershipKey, out var member))
        {
            return null;
        }

        var later = _moves[membershipKey].FirstOrDefault(m => DateOnly.FromDateTime(m.MovedAtUtc) > day);
        return later is null ? member.GroupKey : later.FromGroupKey;
    }

    /// <summary>
    /// Each youth's points in the period, by source. With <paramref name="groupKey"/>, only what was
    /// earned in that гурток — what its page shows, including someone who has since moved on.
    /// </summary>
    public IReadOnlyList<ScorePersonTotal> People(ScorePeriod period, Guid? groupKey = null) =>
        Lines(period)
            .Where(l => groupKey is null || l.GroupKey == groupKey)
            .GroupBy(l => l.MembershipKey)
            .Select(g => new ScorePersonTotal(
                g.Key,
                g.Sum(l => l.Points),
                g.GroupBy(l => l.Source).ToDictionary(s => s.Key, s => s.Sum(l => l.Points))))
            .ToList();

    /// <summary>
    /// How the гуртки stand in the period, best first. Every гурток in <paramref name="groupKeys"/> is
    /// there, a гурток nobody scored in too; one that only had points is added.
    /// </summary>
    public IReadOnlyList<ScoreGroupTotal> Groups(ScorePeriod period, IEnumerable<Guid> groupKeys)
    {
        var end = period.To < _today ? period.To : _today;
        var periodDays = end.DayNumber - period.From.DayNumber + 1;
        var youthDays = periodDays > 0 ? YouthDays(period.From, end) : new Dictionary<Guid, int>();

        var points = Lines(period)
            .Where(l => l.GroupKey.HasValue)
            .GroupBy(l => l.GroupKey!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Points));

        var groupPoints = _groupEntries
            .Where(e => period.Contains(e.OccurredOn))
            .GroupBy(e => e.GroupKey!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Points));

        var keys = groupKeys.Concat(points.Keys).Concat(groupPoints.Keys).Distinct();
        return keys
            .Select(key =>
            {
                var sum = points.GetValueOrDefault(key);
                var count = periodDays > 0 ? Math.Round((decimal)youthDays.GetValueOrDefault(key) / periodDays, 2) : 0;
                var average = count > 0 ? Math.Round(sum / count, 2) : 0;
                var bonus = groupPoints.GetValueOrDefault(key);
                var (main, other) = _algorithm == ScoreAlgorithm.Average ? (average, (decimal)sum) : (sum, average);
                return new ScoreGroupTotal(key, sum, count, average, bonus, main + bonus, other + bonus);
            })
            .OrderByDescending(g => g.Score)
            .ToList();
    }

    private void Add(List<ScoreLine> lines, Guid membershipKey, ScoreSource source, int points, DateOnly on)
    {
        if (points != 0)
        {
            lines.Add(new ScoreLine(membershipKey, GroupOn(membershipKey, on), source, points, on));
        }
    }

    private static int AttendancePoints(
        Guid itemKey,
        IReadOnlyDictionary<Guid, Guid?> eventCategories,
        Dictionary<Guid, int> byItem,
        Dictionary<Guid, int> byCategory)
    {
        if (byItem.TryGetValue(itemKey, out var own))
        {
            return own;
        }

        return eventCategories.GetValueOrDefault(itemKey) is { } category
            ? byCategory.GetValueOrDefault(category)
            : 0;
    }

    /// <summary>How many youth-days each гурток had between the two days, both included.</summary>
    private Dictionary<Guid, int> YouthDays(DateOnly from, DateOnly to)
    {
        var days = new Dictionary<Guid, int>();
        foreach (var member in _members.Values)
        {
            var first = member.JoinedOn > from ? member.JoinedOn : from;
            var last = member.LeftOn is { } left && left < to ? left : to;
            for (var day = first; day <= last; day = day.AddDays(1))
            {
                if (GroupOn(member.MembershipKey, day) is { } group)
                {
                    days[group] = days.GetValueOrDefault(group) + 1;
                }
            }
        }

        return days;
    }
}
