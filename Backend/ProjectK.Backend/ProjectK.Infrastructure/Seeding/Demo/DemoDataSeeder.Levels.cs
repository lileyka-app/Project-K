using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.Infrastructure.Seeding.Demo;

/// <summary>Ступені reached at believable dates, and the profile check the провід has done on them.</summary>
public partial class DemoDataSeeder
{
    private void SeedPlastLevels(IEnumerable<Person> youth, IEnumerable<Person> staff, Guid kurinKey)
    {
        foreach (var person in youth)
        {
            var joined = person.JoinedAtUtc;
            var yearsIn = (_now - joined).TotalDays / 365.0;
            var ladder = new List<(PlastLevel Level, DateTime When)> { (PlastLevel.Entry, joined) };

            if (yearsIn >= 0.6)
            {
                ladder.Add((PlastLevel.Prykhylnyk, joined.AddMonths(6)));
            }

            if (yearsIn >= 1.4 && person.Age >= 12)
            {
                ladder.Add((PlastLevel.Uchasnyk, joined.AddMonths(15)));
            }

            if (yearsIn >= 3.1 && person.Age >= 14)
            {
                ladder.Add((PlastLevel.Rozviduvach, joined.AddMonths(36)));
            }

            if (yearsIn >= 4.6 && person.Age >= 16)
            {
                ladder.Add((PlastLevel.Skob, joined.AddMonths(54)));
            }

            foreach (var (level, when) in ladder)
            {
                _dbContext.PlastLevelHistories.Add(new PlastLevelHistory
                {
                    MemberKey = person.Key,
                    KurinKey = kurinKey,
                    PlastLevel = level,
                    DateAchieved = DateOnly.FromDateTime(when)
                });
            }

            person.Level = ladder[^1].Level;
            person.Member.LatestPlastLevel = person.Level;
        }

        // Staff earned theirs elsewhere, years ago: no kurin on those rows, which is what null means.
        foreach (var person in staff)
        {
            var eleven = person.Member.DateOfBirth.AddYears(11);
            var ladder = new List<(PlastLevel Level, DateOnly When)>
            {
                (PlastLevel.Entry, eleven),
                (PlastLevel.Uchasnyk, eleven.AddYears(1)),
                (PlastLevel.Rozviduvach, eleven.AddYears(3)),
                (PlastLevel.Skob, eleven.AddYears(5)),
                (PlastLevel.Starshoplastun, person.Member.DateOfBirth.AddYears(18))
            };

            if (person.Age >= 35)
            {
                ladder.Add((PlastLevel.Senior, person.Member.DateOfBirth.AddYears(35)));
            }

            foreach (var (level, when) in ladder)
            {
                _dbContext.PlastLevelHistories.Add(new PlastLevelHistory
                {
                    MemberKey = person.Key,
                    KurinKey = null,
                    PlastLevel = level,
                    DateAchieved = when
                });
            }

            person.Level = ladder[^1].Level;
            person.Member.LatestPlastLevel = person.Level;
        }
    }

    private void SeedProfileVerification(IEnumerable<Person> youth, Person zvyazkovyi)
    {
        foreach (var member in youth.Select(person => person.Member))
        {
            var roll = _random.NextDouble();
            if (roll < 0.6)
            {
                member.ProfileVerificationStatus = MemberProfileVerificationStatus.VerifiedCurrent;
            }
            else if (roll < 0.75)
            {
                member.ProfileVerificationStatus = MemberProfileVerificationStatus.VerifiedStale;
            }
            else
            {
                continue;
            }

            member.ProfileVerifiedAtUtc = DaysAgo(_random.Next(10, 300));
            member.ProfileVerifiedByUserKey = zvyazkovyi.UserKey;
        }
    }
}
