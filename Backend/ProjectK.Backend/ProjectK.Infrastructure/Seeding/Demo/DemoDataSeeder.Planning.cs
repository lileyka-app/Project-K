using ProjectK.Common.Entities.KurinModule.Planning;

namespace ProjectK.Infrastructure.Seeding.Demo;

/// <summary>Camp planning: the sessions and when each participant is busy.</summary>
public partial class DemoDataSeeder
{
    private void SeedPlanning(Guid kurinKey, IReadOnlyList<Person> youth, IReadOnlyDictionary<Guid, Person> mentors, Person zvyazkovyi)
    {
        var year = _now.Year;

        var winter = new PlanningSession
        {
            KurinKey = kurinKey,
            Name = $"Зимовий табір {year + 1}",
            CreatedByUserKey = zvyazkovyi.UserKey,
            SearchStart = new DateTime(year, 12, 26, 0, 0, 0, DateTimeKind.Utc),
            SearchEnd = new DateTime(year + 1, 1, 20, 0, 0, 0, DateTimeKind.Utc),
            DurationDays = 7,
            OptimalStartDate = new DateTime(year + 1, 1, 3, 0, 0, 0, DateTimeKind.Utc),
            OptimalEndDate = new DateTime(year + 1, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            ConflictScore = 1.5,
            IsCalculated = true
        };

        winter.Participants.Add(Participant(zvyazkovyi, 2.0, (UtcDay(year, 12, 30), UtcDay(year + 1, 1, 2))));
        foreach (var mentor in mentors.Values)
        {
            winter.Participants.Add(Participant(mentor, 1.5, (UtcDay(year + 1, 1, 12), UtcDay(year + 1, 1, 16))));
        }

        foreach (var person in youth.Take(12))
        {
            var busy = _random.NextDouble() < 0.5
                ? (UtcDay(year, 12, 31), UtcDay(year + 1, 1, 2))
                : (UtcDay(year + 1, 1, 13), UtcDay(year + 1, 1, 18));
            winter.Participants.Add(Participant(person, 1.0, busy));
        }

        _dbContext.PlanningSessions.Add(winter);

        var alps = new PlanningSession
        {
            KurinKey = kurinKey,
            Name = "Мандрівка в Альпи",
            CreatedByUserKey = zvyazkovyi.UserKey,
            SearchStart = new DateTime(year + 1, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            SearchEnd = new DateTime(year + 1, 5, 31, 0, 0, 0, DateTimeKind.Utc),
            DurationDays = 4,
            IsCalculated = false
        };

        foreach (var person in youth.Skip(12).Take(6))
        {
            alps.Participants.Add(Participant(person, 1.0, (UtcDay(year + 1, 5, 8), UtcDay(year + 1, 5, 11))));
        }

        _dbContext.PlanningSessions.Add(alps);
    }

    private static PlanningParticipant Participant(Person person, double weight, (DateTime Start, DateTime End) busy)
    {
        var participant = new PlanningParticipant
        {
            MemberKey = person.Key,
            FullName = person.FullName,
            RoleWeight = weight
        };
        participant.BusyRanges.Add(new ParticipantBusyRange
        {
            Start = DateTime.SpecifyKind(busy.Start, DateTimeKind.Utc),
            End = DateTime.SpecifyKind(busy.End, DateTimeKind.Utc)
        });
        return participant;
    }
}
