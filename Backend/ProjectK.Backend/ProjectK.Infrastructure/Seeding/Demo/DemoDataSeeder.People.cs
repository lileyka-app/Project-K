using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.Infrastructure.Seeding.Demo;

/// <summary>The people of kurin 1: who they are, where they live and go to school, and the ones who have left.</summary>
public partial class DemoDataSeeder
{
    private static readonly string[] FirstNames =
    {
        "Андрій", "Богдан", "Василь", "Григорій", "Дмитро", "Остап", "Ігор", "Тарас",
        "Юрій", "Роман", "Степан", "Микола", "Олег", "Павло", "Сергій", "Назар",
        "Максим", "Орест", "Левко", "Артем", "Данило", "Марко", "Захар", "Устим",
        "Ярослав", "Мирослав", "Святослав", "Володимир", "Любомир", "Ростислав",
        "Матвій", "Тимофій", "Іван", "Михайло", "Олексій", "Віталій"
    };

    private static readonly string[] MiddleNames =
    {
        "Андрійович", "Богданович", "Васильович", "Ігорович", "Тарасович", "Юрійович",
        "Романович", "Степанович", "Миколайович", "Олегович", "Павлович", "Сергійович",
        "Назарович", "Орестович", "Ярославович", "Мирославович", "Володимирович",
        "Любомирович", "Петрович", "Іванович", "Михайлович", "Олександрович", "Зіновійович"
    };

    private static readonly string[] LastNames =
    {
        "Шевченко", "Франко", "Коваль", "Бондаренко", "Мельник", "Ткаченко", "Кравчук", "Гнатюк",
        "Панчук", "Савчук", "Романюк", "Дідух", "Іваненко", "Кузьменко", "Лисенко", "Марчук",
        "Гаврилюк", "Оліярник", "Соловей", "Вербицький", "Гончар", "Пасічник", "Цимбалюк", "Яремчук",
        "Стельмах", "Чорновіл", "Кушнір", "Бойчук", "Сорока", "Левицький",
        "Кміть", "Кропива", "Мартинюк", "Заяць", "Гуцул", "Пилипчук"
    };

    // A diaspora kurin: the станиця is Luxembourg, so the people live on its streets and go to
    // its schools, while the numbers they are reached at stay Ukrainian — that is the only mask
    // the member form knows.
    private static readonly string[] Streets =
    {
        "rue de la Gare", "avenue de la Liberté", "boulevard Royal", "rue Notre-Dame", "rue des Bains",
        "route d'Esch", "rue de Hollerich", "avenue Pasteur", "rue Adolphe Fischer", "rue de Strasbourg",
        "rue du Fort Neipperg", "avenue de la Faïencerie", "rue de Bonnevoie"
    };

    private static readonly string[] Postcodes =
    {
        "1611", "1930", "2449", "2240", "1212", "1470", "1741", "2310", "1520", "2560", "2230", "1510", "1260"
    };

    private static readonly string[] Schools =
    {
        "Athénée de Luxembourg", "Lycée Michel-Rodange", "Lycée de Garçons de Luxembourg",
        "Lycée Aline Mayrisch", "École européenne Luxembourg I", "École européenne Luxembourg II",
        "Lycée Robert-Schuman", "International School of Luxembourg", "Lycée Vauban",
        "Lycée Technique du Centre"
    };

    private static readonly string[] MobileCodes =
    {
        "67", "68", "96", "97", "98", "50", "66", "95", "99", "63", "73", "93"
    };

    private async Task<Person> CreateStaffAsync(Guid kurinKey, int birthYear, int joinedYearsAgo, CancellationToken cancellationToken)
    {
        var dateOfBirth = new DateOnly(birthYear, _random.Next(1, 13), _random.Next(1, 28));
        var joined = _now.AddYears(-joinedYearsAgo).AddDays(-_random.Next(0, 120));
        var member = await DataSeeder.EnsureMemberAsync(
            _dbContext,
            _userManager,
            NextPerson(kurinKey, null, dateOfBirth, MembershipKind.Staff, joined, school: null),
            cancellationToken);

        return new Person(member, null, joined, AgeOf(dateOfBirth));
    }

    private async Task<List<Person>> CreateYouthAsync(
        Guid kurinKey,
        Guid groupKey,
        int count,
        int minAge,
        int maxAge,
        int minYearsInPlast,
        int maxYearsInPlast,
        CancellationToken cancellationToken)
    {
        var people = new List<Person>(count);
        for (var i = 0; i < count; i++)
        {
            var age = _random.Next(minAge, maxAge + 1);
            var dateOfBirth = DateOnly.FromDateTime(_now.AddYears(-age).AddDays(-_random.Next(0, 365)));
            var yearsInPlast = Math.Min(_random.Next(minYearsInPlast, maxYearsInPlast + 1), Math.Max(1, age - 10));
            var joined = _now.AddYears(-yearsInPlast).AddDays(-_random.Next(0, 200));

            var member = await DataSeeder.EnsureMemberAsync(
                _dbContext,
                _userManager,
                NextPerson(kurinKey, groupKey, dateOfBirth, MembershipKind.Youth, joined, Pick(Schools)),
                cancellationToken);

            people.Add(new Person(member, groupKey, joined, age));
        }

        return people;
    }

    /// <summary>
    /// What the kurin's archive shows on the demo: «Беркути», dissolved fourteen months ago, with its
    /// провід and two members who moved on; a писар of three years ago who has since left; and the
    /// instructor's earlier years as Звʼязковий. The stint dates are set by hand afterwards — the
    /// context stamps a move with the moment it is saved.
    /// </summary>
    private async Task SeedArchiveAsync(
        Guid kurinKey,
        Group sokoly,
        Leadership kurinLeadership,
        Leadership kvLeadership,
        Person instructor,
        IReadOnlyList<Person> vedmediYouth,
        CancellationToken cancellationToken)
    {
        var berkuty = await DataSeeder.EnsureGroupAsync(_dbContext, "Беркути", kurinKey, cancellationToken);
        berkuty.Description = "Гурток 2019–2025 років.";
        var dissolvedOn = DateOnly.FromDateTime(_now.AddMonths(-14));
        var berkutyLeadership = await EnsureLeadershipAsync(LeadershipType.Group, null, berkuty.GroupKey, new DateOnly(2021, 9, 1), cancellationToken);
        berkutyLeadership.EndDate = dissolvedOn;

        await CreateFormerAsync(kurinKey, berkuty.GroupKey, berkutyLeadership, LeadershipRole.Hurtkoviy, leftMonthsAgo: 14, cancellationToken, new DateOnly(2022, 9, 1));
        var movedOn = await CreateFormerAsync(kurinKey, berkuty.GroupKey, null, null, leftMonthsAgo: 6, cancellationToken);
        await CreateFormerAsync(kurinKey, sokoly.GroupKey, kurinLeadership, LeadershipRole.Pysar, leftMonthsAgo: 20, cancellationToken, new DateOnly(2022, 9, 1));
        SeatOffice(berkutyLeadership, movedOn.MemberKey, LeadershipRole.Pysar, new DateOnly(2023, 9, 1), dissolvedOn);
        SeatOffice(kurinLeadership, vedmediYouth[^1].Key, LeadershipRole.Skarbnyk, new DateOnly(2023, 9, 1), new DateOnly(2025, 8, 31));
        SeatOffice(kvLeadership, instructor.Key, LeadershipRole.Zvyazkovyi, new DateOnly(2021, 9, 1), new DateOnly(2024, 8, 31));
        await _dbContext.SaveChangesAsync(cancellationToken);

        // The one who moved on went from «Беркути» to «Соколи» when the гурток was dissolved.
        var moving = await _dbContext.Memberships.FirstAsync(ms => ms.MemberKey == movedOn.MemberKey, cancellationToken);
        var leftAt = moving.LeftAtUtc;
        moving.LeftAtUtc = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
        moving.GroupKey = sokoly.GroupKey;
        await _dbContext.SaveChangesAsync(cancellationToken);
        moving.LeftAtUtc = leftAt;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dissolvedAt = dissolvedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        foreach (var stint in await _dbContext.MembershipGroupStints.Where(s => s.MembershipKey == moving.MembershipKey).ToListAsync(cancellationToken))
        {
            if (stint.GroupKey == berkuty.GroupKey)
            {
                stint.ToUtc = dissolvedAt;
            }
            else
            {
                stint.FromUtc = dissolvedAt;
            }
        }

        // A гурток bounces back from a stint closed twice by the reopen above; keep one per гурток.
        var duplicates = await _dbContext.MembershipGroupStints
            .Where(s => s.MembershipKey == moving.MembershipKey && s.GroupKey == berkuty.GroupKey)
            .OrderBy(s => s.FromUtc)
            .Skip(1)
            .ToListAsync(cancellationToken);
        _dbContext.MembershipGroupStints.RemoveRange(duplicates);

        // As dissolving does: nobody's membership points at the гурток any more.
        foreach (var membership in await _dbContext.Memberships.Where(ms => ms.GroupKey == berkuty.GroupKey).ToListAsync(cancellationToken))
        {
            membership.GroupKey = null;
        }

        berkuty.DissolvedAtUtc = dissolvedAt;
    }

    private async Task<Member> CreateFormerAsync(
        Guid kurinKey,
        Guid groupKey,
        Leadership? leadership,
        LeadershipRole? formerOffice,
        int leftMonthsAgo,
        CancellationToken cancellationToken,
        DateOnly? officeSince = null)
    {
        var age = _random.Next(15, 18);
        var dateOfBirth = DateOnly.FromDateTime(_now.AddYears(-age).AddDays(-_random.Next(0, 365)));
        var joined = _now.AddYears(-4).AddDays(-_random.Next(0, 200));
        var member = await DataSeeder.EnsureMemberAsync(
            _dbContext,
            _userManager,
            NextPerson(kurinKey, groupKey, dateOfBirth, MembershipKind.Youth, joined, Pick(Schools)),
            cancellationToken);

        var left = _now.AddMonths(-leftMonthsAgo);
        var membership = await _dbContext.Memberships
            .FirstAsync(ms => ms.MemberKey == member.MemberKey && ms.LeftAtUtc == null, cancellationToken);
        membership.LeftAtUtc = left;

        if (leadership is not null && formerOffice is not null)
        {
            SeatOffice(leadership, member.MemberKey, formerOffice.Value, officeSince ?? new DateOnly(2024, 9, 1), DateOnly.FromDateTime(left));
        }

        _dbContext.PlastLevelHistories.Add(new PlastLevelHistory
        {
            MemberKey = member.MemberKey,
            KurinKey = kurinKey,
            PlastLevel = PlastLevel.Entry,
            DateAchieved = DateOnly.FromDateTime(joined)
        });
        _dbContext.PlastLevelHistories.Add(new PlastLevelHistory
        {
            MemberKey = member.MemberKey,
            KurinKey = kurinKey,
            PlastLevel = PlastLevel.Uchasnyk,
            DateAchieved = DateOnly.FromDateTime(joined.AddMonths(15))
        });
        member.LatestPlastLevel = PlastLevel.Uchasnyk;
        return member;
    }

    private DataSeeder.SeededPerson NextPerson(
        Guid kurinKey,
        Guid? groupKey,
        DateOnly dateOfBirth,
        MembershipKind kind,
        DateTime joinedAtUtc,
        string? school)
    {
        var firstName = _firstNames[_personIndex % _firstNames.Length];
        var lastName = _lastNames[(_personIndex + _personIndex / _lastNames.Length) % _lastNames.Length];
        _personIndex++;

        return new DataSeeder.SeededPerson(
            $"demo{_emailIndex++}@projectk.com",
            firstName,
            Pick(MiddleNames),
            lastName,
            kurinKey,
            groupKey,
            NextPhone(),
            dateOfBirth,
            kind,
            joinedAtUtc,
            $"{_random.Next(1, 140)}, {Pick(Streets)}, L-{Pick(Postcodes)} Люксембург",
            school);
    }

    private string NextPhone()
    {
        while (true)
        {
            var phone = $"+380{Pick(MobileCodes)}{_random.Next(1000000, 9999999)}";
            if (_phones.Add(phone))
            {
                return phone;
            }
        }
    }
}
