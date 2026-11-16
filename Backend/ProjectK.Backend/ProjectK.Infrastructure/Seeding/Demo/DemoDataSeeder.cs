using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Seeding.Demo;

/// <summary>
/// Seeds a kurin that looks lived-in: three гуртки with their провід and виховники, a КВ, people
/// with по батькові, addresses, schools and ступені reached at believable dates, probes signed
/// point by point over months, вмілості confirmed and a few still waiting for review, відзначення,
/// перестороги, a calendar of сходини, табори and заходи with RSVPs, a task board, and camp
/// planning — so every screen has something on it the moment the stack starts.
/// <para>
/// Randomness is seeded, so two runs produce the same kurin: screenshots, fixtures and a colleague's
/// machine all show the same people. Dates are relative to now, so the calendar always has a past
/// and a future. The first account created is the Зв'язковий, which keeps <c>demo0</c> meaning what
/// the docs and the screenshot script say it means.
/// </para>
/// </summary>
public partial class DemoDataSeeder : IDemoDataSeeder
{
    private const int KurinNumber = 1;
    private const string Sokoly = "Соколи";

    private readonly AppDbContext _dbContext;
    private readonly UserManager<AppUser> _userManager;
    private readonly IProgressCatalogReader _catalog;

    private readonly Random _random = new(20260920);
    private readonly DateTime _now = DateTime.UtcNow;
    private readonly HashSet<string> _phones = new();
    private readonly string[] _firstNames;
    private readonly string[] _lastNames;

    private int _personIndex;
    private int _emailIndex;

    public DemoDataSeeder(AppDbContext dbContext, UserManager<AppUser> userManager, IProgressCatalogReader catalog)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _catalog = catalog;
        _firstNames = Shuffled(FirstNames);
        _lastNames = Shuffled(LastNames);
    }

    /// <summary>One seeded person, kept in memory for the passes that reference each other.</summary>
    private sealed record Person(Member Member, Guid? GroupKey, DateTime JoinedAtUtc, int Age)
    {
        public Guid Key => Member.MemberKey;
        public Guid UserKey => Member.UserKey!.Value;
        public string FullName => $"{Member.FirstName} {Member.LastName}";
        public PlastLevel? Level { get; set; }
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var kurin = await EnsureKurinAsync(cancellationToken);
        var kurinKey = kurin.KurinKey;

        var sokoly = await DataSeeder.EnsureGroupAsync(_dbContext, Sokoly, kurinKey, cancellationToken);
        var levy = await DataSeeder.EnsureGroupAsync(_dbContext, "Леви", kurinKey, cancellationToken);
        var vedmedi = await DataSeeder.EnsureGroupAsync(_dbContext, "Ведмеді", kurinKey, cancellationToken);
        sokoly.Description = "Старші юнаки, 14–16 років. Сходини щосереди о 17:00 у домівці.";
        levy.Description = "Молодші юнаки, 12–14 років. Сходини щопʼятниці о 16:00.";
        vedmedi.Description = "Провідний гурток: з його складу обирається курінний провід.";

        var kvLeadership = await EnsureLeadershipAsync(LeadershipType.KV, kurinKey, null, new DateOnly(2024, 9, 1), cancellationToken);

        // 1. Зв'язковий first: demo0 is the account the docs and the screenshot script sign in with.
        var zvyazkovyi = await CreateStaffAsync(kurinKey, birthYear: 1986, joinedYearsAgo: 2, cancellationToken);
        SeatOffice(kvLeadership, zvyazkovyi.Key, LeadershipRole.Zvyazkovyi, new DateOnly(2024, 9, 1));

        // 2. Ordinary гуртки: the first six hold the гуртковий провід since last September.
        var sokolyYouth = await CreateYouthAsync(kurinKey, sokoly.GroupKey, 9, minAge: 14, maxAge: 16, minYearsInPlast: 2, maxYearsInPlast: 5, cancellationToken);
        var levyYouth = await CreateYouthAsync(kurinKey, levy.GroupKey, 8, minAge: 12, maxAge: 14, minYearsInPlast: 1, maxYearsInPlast: 3, cancellationToken);

        var sokolyLeadership = await EnsureLeadershipAsync(LeadershipType.Group, null, sokoly.GroupKey, new DateOnly(2025, 9, 1), cancellationToken);
        var levyLeadership = await EnsureLeadershipAsync(LeadershipType.Group, null, levy.GroupKey, new DateOnly(2025, 9, 1), cancellationToken);
        SeatOffices(sokolyLeadership, sokolyYouth, GroupOffices, new DateOnly(2025, 9, 1));
        SeatOffices(levyLeadership, levyYouth, GroupOffices, new DateOnly(2025, 9, 1));

        // Last year's гуртковий of Соколи handed over: a closed row is what the history table shows.
        SeatOffice(sokolyLeadership, sokolyYouth[6].Key, LeadershipRole.Hurtkoviy, new DateOnly(2024, 9, 1), new DateOnly(2025, 8, 31));

        // 3. Провідний гурток "Ведмеді": its members form the курінний провід.
        var vedmediYouth = await CreateYouthAsync(kurinKey, vedmedi.GroupKey, 8, minAge: 15, maxAge: 17, minYearsInPlast: 3, maxYearsInPlast: 5, cancellationToken);
        var kurinLeadership = await EnsureLeadershipAsync(LeadershipType.Kurin, kurinKey, null, new DateOnly(2025, 9, 1), cancellationToken);
        var kurinOfficeHolders = SeatOffices(kurinLeadership, vedmediYouth, KurinOffices, new DateOnly(2025, 9, 1));

        // 4. One Впорядник per гурток: a КВ office plus the mentor assignment that scopes their access.
        var mentors = new Dictionary<Guid, Person>();
        foreach (var (group, sinceYears) in new[] { (sokoly, 3), (levy, 1), (vedmedi, 4) })
        {
            var mentor = await CreateStaffAsync(kurinKey, birthYear: _random.Next(1994, 2002), joinedYearsAgo: sinceYears, cancellationToken);
            SeatOffice(kvLeadership, mentor.Key, LeadershipRole.Vykhovnyk, YearsAgo(sinceYears));
            _dbContext.MentorAssignments.Add(new MentorAssignment
            {
                MentorUserKey = mentor.UserKey,
                GroupKey = group.GroupKey,
                AssignedAtUtc = _now.AddYears(-sinceYears).AddDays(-_random.Next(0, 30))
            });
            mentors[group.GroupKey] = mentor;
        }

        var instructor = await CreateStaffAsync(kurinKey, birthYear: 1990, joinedYearsAgo: 2, cancellationToken);
        SeatOffice(kvLeadership, instructor.Key, LeadershipRole.Instruktor, YearsAgo(2));

        // 5. Two people who left: the реєстр has a «Колишні» section for a reason.
        await CreateFormerAsync(kurinKey, sokoly.GroupKey, sokolyLeadership, LeadershipRole.Pysar, leftMonthsAgo: 4, cancellationToken);
        await CreateFormerAsync(kurinKey, levy.GroupKey, null, null, leftMonthsAgo: 11, cancellationToken);

        // 6. The archive: a гурток dissolved last year with its провід, and who held the kurin's roles before.
        await SeedArchiveAsync(kurinKey, sokoly, kurinLeadership, kvLeadership, instructor, vedmediYouth, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var youth = sokolyYouth.Concat(levyYouth).Concat(vedmediYouth).ToList();
        var staff = new List<Person> { zvyazkovyi, instructor };
        staff.AddRange(mentors.Values);

        SeedPlastLevels(youth, staff, kurinKey);
        SeedProfileVerification(youth, zvyazkovyi);
        SeedProgress(youth, mentors, zvyazkovyi, kurinKey);
        SeedAwards(youth, mentors, zvyazkovyi, kurinKey);
        SeedWarnings(youth, mentors, zvyazkovyi, kurinKey);
        SeedAccessPolicy(kurin, zvyazkovyi);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var groups = new Dictionary<string, Group> { [Sokoly] = sokoly, ["Леви"] = levy, ["Ведмеді"] = vedmedi };
        var leaderships = new Dictionary<string, Leadership>
        {
            ["KV"] = kvLeadership,
            ["Kurin"] = kurinLeadership,
            [Sokoly] = sokolyLeadership,
            ["Леви"] = levyLeadership
        };
        SeedAgenda(kurinKey, groups, leaderships, youth, mentors, zvyazkovyi, kurinOfficeHolders);
        SeedPlanning(kurinKey, youth, mentors, zvyazkovyi);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var memberships = await _dbContext.Memberships
            .Where(ms => ms.KurinKey == kurinKey)
            .ToDictionaryAsync(ms => ms.MemberKey, ms => ms.MembershipKey, cancellationToken);
        SeedDues(kurinKey, groups, youth, mentors, zvyazkovyi, memberships);
        SeedScore(kurinKey, groups, youth, mentors, zvyazkovyi, memberships);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await SeedSeniorKurinsAsync(cancellationToken);
        await SeedLifeStoryAsync(kurinKey, vedmedi, kurinLeadership, mentors[vedmedi.GroupKey], zvyazkovyi, cancellationToken);
    }

    private async Task<Kurin> EnsureKurinAsync(CancellationToken cancellationToken)
    {
        var kurin = await _dbContext.Kurins.FirstOrDefaultAsync(k => k.KraiCode == Krai.Ukraine && k.Branch == KurinBranch.UPYu && k.Number == KurinNumber, cancellationToken);
        if (kurin == null)
        {
            kurin = new Kurin(KurinNumber);
            _dbContext.Kurins.Add(kurin);
        }

        kurin.NamedAfter = "Івана Богуна";
        kurin.Stanytsia = "Люксембург";
        kurin.RegionOrCountry = "Люксембург";
        kurin.SetBranches([new KurinNumbering(KurinBranch.UPYu, KurinNumber)]);
        kurin.ProfileVerificationEnabled = true;
        kurin.Description =
            "Курінь УПЮ при станиці Люксембург. Три гуртки, сходини щосуботи в домівці в Бонневуа, "
            + "літній табір в Арденнах і зимовий — у Вогезах. Заснований 2022 року українською "
            + "громадою Люксембургу.";

        await _dbContext.SaveChangesAsync(cancellationToken);
        return kurin;
    }
}
