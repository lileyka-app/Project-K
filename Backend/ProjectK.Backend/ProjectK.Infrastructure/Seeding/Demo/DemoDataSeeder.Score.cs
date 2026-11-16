using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.ScoreModule;
using ProjectK.Common.Rules.DuesModule;

namespace ProjectK.Infrastructure.Seeding.Demo;

/// <summary>A year of точкування in kurin 1.</summary>
public partial class DemoDataSeeder
{
    private static readonly string[] PrivateCriteria = { "Лідерство", "Ініціатива", "Відповідальність" };

    /// <summary>
    /// The score of a year: what each source is worth, marks of attendance on every сходини since
    /// the last year opened and at the summer camp, positions given at сходини, a few free entries
    /// for persons and гуртки, two stages, and the КВ's own book.
    /// </summary>
    private void SeedScore(
        Guid kurinKey,
        IReadOnlyDictionary<string, Group> groups,
        IReadOnlyList<Person> youth,
        IReadOnlyDictionary<Guid, Person> mentors,
        Person zvyazkovyi,
        IReadOnlyDictionary<Guid, Guid> memberships)
    {
        var today = DateOnly.FromDateTime(_now);
        var since = PlastYearRules.FirstDay(PlastYearRules.Of(today) - 1);
        var judge = zvyazkovyi.UserKey;

        _dbContext.KurinScoreSettings.Add(new KurinScoreSettings { KurinKey = kurinKey, Algorithm = ScoreAlgorithm.Average, SetByUserKey = judge });

        foreach (var (source, variant, points) in new[]
        {
            (ScoreSource.Skill, 0, 5), (ScoreSource.ProbePoint, 0, 1), (ScoreSource.Probe, 0, 10), (ScoreSource.Dues, 0, 3),
            (ScoreSource.Warning, 1, -3), (ScoreSource.Warning, 2, -6), (ScoreSource.Warning, 3, -10)
        })
        {
            _dbContext.ScoreRules.Add(new ScoreRule
            {
                KurinKey = kurinKey,
                Source = source,
                Variant = variant,
                FromDate = since,
                Points = points,
                SetByUserKey = judge
            });
        }

        var skhodyny = _dbContext.AgendaCategories.Local.Single(c => c.KurinKey == kurinKey && c.Name == "Сходини");
        _dbContext.ScoreAttendanceRates.Add(new ScoreAttendanceRate { KurinKey = kurinKey, AgendaCategoryKey = skhodyny.AgendaCategoryKey, Points = 2, SetByUserKey = judge });

        var positions = new[] { ("Однострій", 1), ("Нотатник", 1), ("Запізнення", -1), ("Без однострою", -2) }
            .Select(p => new ScoreItem { KurinKey = kurinKey, Name = p.Item1, Points = p.Item2 })
            .ToList();
        _dbContext.ScoreItems.AddRange(positions);

        var reliability = youth.ToDictionary(p => p.Key, _ => Fraction(0.55, 0.95));

        var weekly = _dbContext.AgendaItems.Local
            .Where(i => i.KurinKey == kurinKey && i.RecurrenceFrequency == RecurrenceFrequency.Weekly && i.StartUtc.HasValue)
            .ToList();
        foreach (var item in weekly)
        {
            var target = item.Assignments.First();
            var isGroup = target.TargetType == AgendaTargetType.Group;
            var people = isGroup ? youth.Where(p => p.GroupKey == target.TargetKey).ToList() : youth.ToList();
            var marker = isGroup ? mentors[target.TargetKey].UserKey : judge;
            var local = TimeZoneInfo.ConvertTimeFromUtc(item.StartUtc!.Value, Kyiv);

            for (var day = local.Date; ToUtc(day, local.Hour) <= _now; day = day.AddDays(7))
            {
                var start = ToUtc(day, local.Hour);
                if (DateOnly.FromDateTime(day) < since)
                {
                    continue;
                }

                foreach (var membershipKey in people.Where(p => _random.NextDouble() < reliability[p.Key]).Select(p => memberships[p.Key]))
                {
                    _dbContext.ScoreAttendances.Add(new ScoreAttendance
                    {
                        KurinKey = kurinKey,
                        MembershipKey = membershipKey,
                        AgendaItemKey = item.AgendaItemKey,
                        OccurrenceStartUtc = start,
                        MarkedByUserKey = marker,
                        MarkedAtUtc = start.AddHours(2)
                    });

                    // A position now and then, given at the гурток's own сходини: once per person there.
                    if (isGroup && _random.NextDouble() < 0.25)
                    {
                        var position = _random.NextDouble() < 0.7 ? positions[_random.Next(0, 2)] : positions[_random.Next(2, 4)];
                        _dbContext.ScoreEntries.Add(new ScoreEntry
                        {
                            KurinKey = kurinKey,
                            MembershipKey = membershipKey,
                            ScoreItemKey = position.ScoreItemKey,
                            Points = position.Points,
                            AgendaItemKey = item.AgendaItemKey,
                            OccurrenceStartUtc = start,
                            OccurredOn = DateOnly.FromDateTime(day),
                            CreatedByUserKey = marker,
                            CreatedDate = start.AddHours(2)
                        });
                    }
                }
            }
        }

        // The summer camp is worth more than a сходини, and those who said they were going went.
        var camp = _dbContext.AgendaItems.Local.Single(i => i.KurinKey == kurinKey && i.Title.StartsWith("Літній табір", StringComparison.Ordinal));
        if (DateOnly.FromDateTime(camp.StartUtc!.Value) >= since && camp.StartUtc < _now)
        {
            _dbContext.ScoreAttendanceRates.Add(new ScoreAttendanceRate { KurinKey = kurinKey, AgendaItemKey = camp.AgendaItemKey, Points = 15, SetByUserKey = judge });
            var going = camp.Responses.Where(r => r.Status == AgendaRsvpStatus.Going).Select(r => r.UserKey).ToHashSet();
            foreach (var memberKey in youth.Where(p => going.Contains(p.UserKey)).Select(p => p.Key))
            {
                _dbContext.ScoreAttendances.Add(new ScoreAttendance
                {
                    KurinKey = kurinKey,
                    MembershipKey = memberships[memberKey],
                    AgendaItemKey = camp.AgendaItemKey,
                    OccurrenceStartUtc = camp.StartUtc.Value,
                    MarkedByUserKey = judge,
                    MarkedAtUtc = camp.EndUtc ?? camp.StartUtc.Value
                });
            }
        }

        var reasons = new[]
        {
            ("Допоміг молодшим на вишколі", 4), ("Підготував гру для гуртка", 5), ("Чергування на таборі", 3),
            ("Хроніка сходин", 2), ("Не прибрав за собою в домівці", -2)
        };
        foreach (var groupKey in groups.Values.Select(g => g.GroupKey))
        {
            var mentor = mentors[groupKey];
            foreach (var person in youth.Where(p => p.GroupKey == groupKey).Take(4))
            {
                var (reason, points) = reasons[_random.Next(reasons.Length)];
                var on = today.AddDays(-_random.Next(3, 45));
                _dbContext.ScoreEntries.Add(new ScoreEntry
                {
                    KurinKey = kurinKey,
                    MembershipKey = memberships[person.Key],
                    Points = points,
                    Reason = reason,
                    OccurredOn = on,
                    CreatedByUserKey = mentor.UserKey,
                    CreatedDate = ToUtc(on.ToDateTime(TimeOnly.MinValue), 20)
                });
            }
        }

        foreach (var (name, points, reason, daysAgo) in new[]
        {
            (Sokoly, 8, "Перемога в естафеті на сходинах куреня", 9),
            ("Ведмеді", 10, "Найкраща хроніка літнього табору", 30),
            ("Леви", -3, "Залишили домівку неприбраною", 20)
        })
        {
            var on = today.AddDays(-daysAgo);
            _dbContext.ScoreEntries.Add(new ScoreEntry
            {
                KurinKey = kurinKey,
                GroupKey = groups[name].GroupKey,
                Points = points,
                Reason = reason,
                OccurredOn = on,
                CreatedByUserKey = judge,
                CreatedDate = ToUtc(on.ToDateTime(TimeOnly.MinValue), 20)
            });
        }

        var year = _now.Year;
        _dbContext.ScoreStages.Add(new ScoreStage { KurinKey = kurinKey, Name = "Літо й табір", FromDate = new DateOnly(year, 6, 1), ToDate = new DateOnly(year, 8, 31) });
        _dbContext.ScoreStages.Add(new ScoreStage { KurinKey = kurinKey, Name = "Осінь", FromDate = new DateOnly(year, 9, 1), ToDate = new DateOnly(year, 11, 30) });

        var criteria = PrivateCriteria
            .Select(name => new PrivateScoreCriterion { KurinKey = kurinKey, Name = name })
            .ToList();
        _dbContext.PrivateScoreCriteria.AddRange(criteria);

        foreach (var person in youth.Where(p => p.GroupKey == groups["Ведмеді"].GroupKey || p.GroupKey == groups[Sokoly].GroupKey).Take(8))
        {
            var on = today.AddDays(-_random.Next(5, 60));
            _dbContext.PrivateScoreEntries.Add(new PrivateScoreEntry
            {
                KurinKey = kurinKey,
                MembershipKey = memberships[person.Key],
                PrivateScoreCriterionKey = criteria[_random.Next(criteria.Count)].PrivateScoreCriterionKey,
                Points = _random.Next(1, 4),
                Note = _random.NextDouble() < 0.5 ? "Сам зголосився провести гру" : null,
                OccurredOn = on,
                CreatedByUserKey = mentors[person.GroupKey!.Value].UserKey,
                CreatedDate = ToUtc(on.ToDateTime(TimeOnly.MinValue), 21)
            });
        }
    }
}
