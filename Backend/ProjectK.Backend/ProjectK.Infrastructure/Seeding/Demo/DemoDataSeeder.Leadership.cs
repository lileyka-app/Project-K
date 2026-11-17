using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.Infrastructure.Seeding.Demo;

/// <summary>Провід: the leaderships and who holds which office, and since when.</summary>
public partial class DemoDataSeeder
{
    // Taken from the office registry rather than restated: the local copy had already lost
    // Hronikar from the гуртковий провід, so demo data no longer matched what the app allows.
    // The «інша роль» placeholders are left empty: a seat with no name is not a провід anyone has.
    private static readonly LeadershipRole[] GroupOffices =
        LeadershipOfficeRules.Grouping[LeadershipType.Group]
            .Where(role => role != LeadershipRole.OtherGroup)
            .ToArray();

    private static readonly LeadershipRole[] KurinOffices =
        LeadershipOfficeRules.Grouping[LeadershipType.Kurin]
            .Where(role => role != LeadershipRole.OtherKurin)
            .ToArray();

    private async Task<Leadership> EnsureLeadershipAsync(
        LeadershipType type,
        Guid? kurinKey,
        Guid? groupKey,
        DateOnly startDate,
        CancellationToken cancellationToken)
    {
        var leadership = await _dbContext.Leaderships
            .Include(l => l.LeadershipHistories)
            .FirstOrDefaultAsync(l => l.Type == type
                && (kurinKey == null || l.KurinKey == kurinKey)
                && (groupKey == null || l.GroupKey == groupKey), cancellationToken);

        if (leadership == null)
        {
            leadership = new Leadership
            {
                Type = type,
                KurinKey = kurinKey,
                GroupKey = groupKey,
                StartDate = startDate
            };
            _dbContext.Leaderships.Add(leadership);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return leadership;
    }

    private static Dictionary<LeadershipRole, Person> SeatOffices(Leadership leadership, IReadOnlyList<Person> people, LeadershipRole[] offices, DateOnly since)
    {
        var holders = new Dictionary<LeadershipRole, Person>();
        for (var i = 0; i < people.Count && i < offices.Length; i++)
        {
            SeatOffice(leadership, people[i].Key, offices[i], since);
            holders[offices[i]] = people[i];
        }

        return holders;
    }

    /// <summary>
    /// Seats a person in an office from a given day — <c>AddOffice</c> dates every row today, which
    /// no каденція does. A closed row (with <paramref name="until"/>) is history, not a current seat.
    /// </summary>
    private static void SeatOffice(Leadership leadership, Guid memberKey, LeadershipRole role, DateOnly since, DateOnly? until = null)
    {
        var existing = leadership.LeadershipHistories
            .FirstOrDefault(h => h.MemberKey == memberKey && h.Role == role && h.EndDate == until);
        if (existing is not null)
        {
            existing.StartDate = since;
            return;
        }

        leadership.LeadershipHistories.Add(new LeadershipHistory
        {
            MemberKey = memberKey,
            Role = role,
            StartDate = since,
            EndDate = until
        });
    }
}
