using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.KurinModule.Tags;
using ProjectK.Common.Models.Tags;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.Infrastructure.Seeding;

/// <summary>
/// Senior kurins beside the УПЮ one: Орден Хрестоносців, one kurin in two гілки (5-й УСП and 20-й
/// УПС), a УСП kurin waiting on its request to join УПС too, and a senior kurin's application on the
/// waitlist. Their Зв'язкові have fixed addresses so the demo can name them:
/// <c>orden@projectk.com</c> and <c>chorty@projectk.com</c>.
/// </summary>
public partial class DemoDataSeeder
{
    internal const string OrdenEmail = "orden@projectk.com";
    internal const string ChortyEmail = "chorty@projectk.com";

    /// <summary>The Зв'язковий of the УПЮ kurin that shares the Order's number in УСП.</summary>
    internal const string SameNumberYouthEmail = "kurin5@projectk.com";

    private async Task SeedSeniorKurinsAsync(CancellationToken cancellationToken)
    {
        var orden = await EnsureSeniorKurinAsync(
            [new KurinNumbering(KurinBranch.USP, 5), new KurinNumbering(KurinBranch.UPS, 20)],
            "Орден Хрестоносців",
            "Львів",
            "Старший курінь у двох уладах: 5-й курінь УСП і 20-й курінь УПС. Один склад, одна домівка, "
            + "сходини щочетверга й мандрівки Карпатами.",
            cancellationToken);
        var ordenLeader = await EnsureSeniorLeaderAsync(orden.KurinKey, OrdenEmail, "Остап", "Гнатюк", 1979, cancellationToken);
        var ordenMembers = new List<Member>();
        foreach (var (first, last, login, year) in OrdenMembers)
        {
            ordenMembers.Add(await EnsureSeniorMemberAsync(orden.KurinKey, first, last, $"orden.{login}@projectk.com", year, cancellationToken));
        }

        // The Order calls its писар «Скриптор»: same role, same rights, its own word.
        orden.RoleNames = KurinRoleNames.Normalize(new Dictionary<string, string?> { [nameof(LeadershipRole.Pysar)] = "Скриптор" }, out _);
        var ordenLeadership = await EnsureLeadershipAsync(LeadershipType.Kurin, orden.KurinKey, null, new DateOnly(2025, 9, 1), cancellationToken);
        SeatOffice(ordenLeadership, ordenMembers[1].MemberKey, LeadershipRole.Pysar, new DateOnly(2025, 9, 1));
        await SeedOrdenCardsAsync(orden.KurinKey, ordenMembers, cancellationToken);
        await SeedOrdenHonoursAsync(orden.KurinKey, ordenLeader, ordenMembers, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await SeedSeniorDuesAsync(orden, ordenLeader, ordenMembers, ordenLeadership, cancellationToken);

        var chorty = await EnsureSeniorKurinAsync(
            [new KurinNumbering(KurinBranch.USP, 7)],
            "Лісові Чорти",
            "Тернопіль",
            "Курінь УСП. Від минулого року частина куреня вже сеньйори, тож курінь просить і числа в УПС.",
            cancellationToken);
        var chortyLink = await EnsureSeniorLeaderAsync(chorty.KurinKey, ChortyEmail, "Марта", "Ковалишин", 1984, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (!await _dbContext.KurinBranchRequests.AnyAsync(r => r.KurinKey == chorty.KurinKey, cancellationToken))
        {
            _dbContext.KurinBranchRequests.Add(new KurinBranchRequest
            {
                KurinKey = chorty.KurinKey,
                RequestedByUserKey = chortyLink.UserKey!.Value,
                UspNumber = 7,
                UpsNumber = 12,
                Note = "Половина куреня вже сеньйори, а число 12 в УПС вільне — підтверджено з краєм.",
                RequestedAtUtc = _now.AddDays(-1)
            });
        }

        await SeedSameNumberYouthKurinAsync(cancellationToken);

        const string applicantEmail = "kulchytskyi@projectk.com";
        if (!await _dbContext.WaitlistEntries.AnyAsync(e => e.Email == applicantEmail, cancellationToken))
        {
            _dbContext.WaitlistEntries.Add(new WaitlistEntry
            {
                WaitlistEntryKey = Guid.NewGuid(),
                FirstName = "Ярема",
                LastName = "Кульчицький",
                Email = applicantEmail,
                PhoneNumber = "+380671234508",
                DateOfBirth = new DateTime(1981, 3, 14),
                Stanytsia = "Івано-Франківськ",
                KraiCode = Krai.Ukraine,
                IsKurinLeaderCandidate = true,
                ClaimedUspNumber = 9,
                ClaimedUpsNumber = 31,
                RequestedAtUtc = _now.AddHours(-5)
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// A УПЮ kurin with the Order's УСП number. Each улад counts its own, so 5-й курінь УПЮ and 5-й
    /// курінь УСП are two kurins, and the admin pages have to keep them apart. Its зв'язкова is a
    /// сеніорка with her вірлиця of 2002 on record: her card shows the сеніорка, and she keeps her own
    /// senior відзначення in this УПЮ kurin.
    /// </summary>
    private async Task SeedSameNumberYouthKurinAsync(CancellationToken cancellationToken)
    {
        var kurin = await EnsureSeniorKurinAsync(
            [new KurinNumbering(KurinBranch.UPYu, 5)],
            "Олени Степанів",
            "Львів",
            "Курінь УПЮ. Сходини щосуботи в домівці на Личаківській.",
            cancellationToken);
        var leader = await EnsureSeniorLeaderAsync(kurin.KurinKey, SameNumberYouthEmail, "Соломія", "Дмитрук", 1990, cancellationToken);
        if (!await _dbContext.PlastLevelHistories.AnyAsync(h => h.MemberKey == leader.MemberKey, cancellationToken))
        {
            foreach (var (level, on) in new[]
            {
                (PlastLevel.Uchasnyk, new DateOnly(2001, 5, 12)),
                (PlastLevel.Skob, new DateOnly(2002, 7, 20)),
                (PlastLevel.Starshoplastun, new DateOnly(2008, 11, 15)),
                (PlastLevel.Senior, new DateOnly(2015, 4, 26))
            })
            {
                _dbContext.PlastLevelHistories.Add(new PlastLevelHistory { MemberKey = leader.MemberKey, KurinKey = kurin.KurinKey, PlastLevel = level, DateAchieved = on, IsPlastunka = true });
            }

            leader.LatestPlastLevel = PlastLevel.Senior;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// What a senior card shows besides the kurin and the role: tags (on in senior kurins by default)
    /// and a пересторога still running beside one that has run out, so the card has both kinds.
    /// </summary>
    private async Task SeedOrdenCardsAsync(Guid kurinKey, IReadOnlyList<Member> members, CancellationToken cancellationToken)
    {
        if (await _dbContext.KurinTags.AnyAsync(t => t.KurinKey == kurinKey, cancellationToken))
        {
            return;
        }

        var palette = KurinTagRules.Palette;
        var mountains = new KurinTag { KurinKey = kurinKey, Name = "Мандрівники", ColorHex = palette[0] };
        var council = new KurinTag { KurinKey = kurinKey, Name = "Рада куреня", ColorHex = palette[2] };
        var seniors = new KurinTag { KurinKey = kurinKey, Name = "Сеніори", ColorHex = palette[4] };
        _dbContext.KurinTags.AddRange(mountains, council, seniors);
        foreach (var (member, tag) in new[] { (members[0], mountains), (members[1], council), (members[1], mountains), (members[2], seniors), (members[3], seniors) })
        {
            _dbContext.MemberTags.Add(new MemberTag { KurinTagKey = tag.KurinTagKey, MemberKey = member.MemberKey });
        }

        // Their ступені: старші пластуни, двоє старших уже сеніори.
        var levels = new[] { PlastLevel.Starshoplastun, PlastLevel.Starshoplastun, PlastLevel.Senior, PlastLevel.SeniorPratsi, PlastLevel.Starshoplastun };
        for (var i = 0; i < members.Count; i++)
        {
            members[i].LatestPlastLevel = levels[i % levels.Length];
            _dbContext.PlastLevelHistories.Add(new PlastLevelHistory
            {
                MemberKey = members[i].MemberKey,
                KurinKey = kurinKey,
                PlastLevel = levels[i % levels.Length],
                DateAchieved = DateOnly.FromDateTime(_now.AddYears(-2 - i))
            });
        }

        var issuer = await _dbContext.Users.Where(u => u.Email == OrdenEmail).Select(u => u.Id).FirstAsync(cancellationToken);
        _dbContext.MemberWarnings.AddRange(
            new MemberWarning
            {
                MemberKey = members[0].MemberKey,
                KurinKey = kurinKey,
                Level = MemberWarningLevel.Level1,
                IssuedAtUtc = _now.AddDays(-12),
                ExpiresAtUtc = _now.AddDays(78),
                IssuedByUserKey = issuer
            },
            new MemberWarning
            {
                MemberKey = members[0].MemberKey,
                KurinKey = kurinKey,
                Level = MemberWarningLevel.Level1,
                IssuedAtUtc = _now.AddMonths(-9),
                ExpiresAtUtc = _now.AddMonths(-6),
                IssuedByUserKey = issuer
            });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    internal const string LifeStoryEmail = "orden.kozak@projectk.com";

    /// <summary>
    /// A senior of the Order who grew up in kurin 1: «Ведмеді» from 2015, the курінний провід's
    /// писар, two probes, вмілості and an УПЮ відзначення, then старший пластун and the Order. His
    /// card shows none of that; his «Пластовий життєпис» shows all of it.
    /// </summary>
    private async Task SeedLifeStoryAsync(
        Guid youthKurinKey,
        Group vedmedi,
        Leadership youthKurinLeadership,
        Person mentor,
        Person zvyazkovyi,
        CancellationToken cancellationToken)
    {
        var orden = await _dbContext.Kurins.FirstAsync(k => k.NamedAfter == "Орден Хрестоносців", cancellationToken);
        if (await _dbContext.Users.AnyAsync(u => u.Email == LifeStoryEmail, cancellationToken))
        {
            return;
        }
        var joined = new DateTime(2015, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var member = await DataSeeder.EnsureMemberAsync(
            _dbContext,
            _userManager,
            new DataSeeder.SeededPerson(LifeStoryEmail, "Андрій", "Тарасович", "Козак", youthKurinKey, vedmedi.GroupKey, NextPhone(), new DateOnly(2004, 5, 14), MembershipKind.Youth, joined),
            cancellationToken);

        var youth = await _dbContext.Memberships.FirstAsync(ms => ms.MemberKey == member.MemberKey && ms.KurinKey == youthKurinKey, cancellationToken);
        youth.LeftAtUtc = new DateTime(2022, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        _dbContext.Memberships.Add(new Membership
        {
            MemberKey = member.MemberKey,
            UserKey = member.UserKey,
            KurinKey = orden.KurinKey,
            Kind = MembershipKind.Youth,
            JoinedAtUtc = new DateTime(2022, 9, 15, 0, 0, 0, DateTimeKind.Utc)
        });

        foreach (var (level, when, kurinKey) in new (PlastLevel, DateOnly, Guid?)[]
                 {
                     (PlastLevel.Entry, new DateOnly(2015, 9, 1), youthKurinKey),
                     (PlastLevel.Uchasnyk, new DateOnly(2016, 10, 8), youthKurinKey),
                     (PlastLevel.Rozviduvach, new DateOnly(2018, 6, 2), youthKurinKey),
                     (PlastLevel.Skob, new DateOnly(2020, 7, 18), youthKurinKey),
                     (PlastLevel.Starshoplastun, new DateOnly(2022, 10, 1), orden.KurinKey)
                 })
        {
            _dbContext.PlastLevelHistories.Add(new PlastLevelHistory { MemberKey = member.MemberKey, KurinKey = kurinKey, PlastLevel = level, DateAchieved = when });
        }

        member.LatestPlastLevel = PlastLevel.Starshoplastun;
        SeatOffice(youthKurinLeadership, member.MemberKey, LeadershipRole.Pysar, new DateOnly(2020, 9, 1), new DateOnly(2022, 6, 30));

        var person = new Person(member, vedmedi.GroupKey, joined, 22) { Level = PlastLevel.Skob };
        var probes = _catalog.GetProbes();
        if (probes.Count > 0)
        {
            SeedProbesFor(person, probes, mentor, zvyazkovyi, youthKurinKey);
        }

        var badgeIds = _catalog.GetBadgeIds();
        if (badgeIds.Count > 0)
        {
            SeedBadgesFor(person, badgeIds, mentor, youthKurinKey, awaitingReview: false, refused: false);
        }

        var camp = new DateTime(2019, 7, 20, 0, 0, 0, DateTimeKind.Utc);
        _dbContext.MemberAwards.Add(new MemberAward
        {
            MemberKey = member.MemberKey,
            KurinKey = youthKurinKey,
            Level = MemberAwardLevel.First,
            DateAcquired = camp,
            Note = "За організацію гурткової мандрівки",
            Status = BadgeProgressStatus.Confirmed,
            SubmittedAtUtc = camp.AddDays(3),
            SubmittedByUserKey = mentor.UserKey,
            ReviewedAtUtc = camp.AddDays(5),
            ReviewedByUserKey = zvyazkovyi.UserKey
        });

        // And one of the Order's, recorded by himself as senior відзначення are.
        var hramota = new DateTime(2024, 5, 18, 0, 0, 0, DateTimeKind.Utc);
        _dbContext.MemberAwards.Add(new MemberAward
        {
            MemberKey = member.MemberKey,
            KurinKey = orden.KurinKey,
            CatalogId = "usp-hramota-zasluhy",
            DateAcquired = hramota,
            Note = "За мандрівку Чорногорою",
            Status = BadgeProgressStatus.Confirmed,
            SubmittedAtUtc = hramota,
            SubmittedByUserKey = member.UserKey
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The Order's ступені and відзначення as its people would record them: the зв'язковий's whole
    /// senior way, and the пластунки's ступені drawn as theirs. Runs on its own guard, so a stand seeded
    /// before senior відзначення existed gets them too.
    /// </summary>
    private async Task SeedOrdenHonoursAsync(Guid kurinKey, Member leader, IReadOnlyList<Member> members, CancellationToken cancellationToken)
    {
        if (await _dbContext.MemberAwards.AnyAsync(a => a.MemberKey == leader.MemberKey && a.CatalogId != null, cancellationToken))
        {
            return;
        }
        var plastunky = new[] { members[1].MemberKey, members[3].MemberKey };
        foreach (var level in await _dbContext.PlastLevelHistories.Where(h => plastunky.Contains(h.MemberKey)).ToListAsync(cancellationToken))
        {
            level.IsPlastunka = true;
        }

        if (!await _dbContext.PlastLevelHistories.AnyAsync(h => h.MemberKey == leader.MemberKey, cancellationToken))
        {
            foreach (var (level, on) in new[]
            {
                (PlastLevel.Skob, new DateOnly(1995, 6, 10)),
                (PlastLevel.Starshoplastun, new DateOnly(1997, 11, 15)),
                (PlastLevel.Senior, new DateOnly(2014, 4, 26)),
                (PlastLevel.SeniorPratsi, new DateOnly(2016, 5, 14))
            })
            {
                _dbContext.PlastLevelHistories.Add(new PlastLevelHistory { MemberKey = leader.MemberKey, KurinKey = kurinKey, PlastLevel = level, DateAchieved = on });
            }

            leader.LatestPlastLevel = PlastLevel.SeniorPratsi;
        }

        MemberAward Award(Member who, string catalogId, DateTime on, string? note = null) => new()
        {
            MemberKey = who.MemberKey,
            KurinKey = kurinKey,
            CatalogId = catalogId,
            DateAcquired = on,
            Note = note,
            Status = BadgeProgressStatus.Confirmed,
            SubmittedAtUtc = on,
            SubmittedByUserKey = who.UserKey
        };

        static MemberAward Pinned(MemberAward award)
        {
            award.IsPinned = true;
            return award;
        }

        _dbContext.MemberAwards.AddRange(
            Award(leader, "usp-hramota-zasluhy", new DateTime(2012, 10, 20), "За табір «Вогні Карпат»"),
            Pinned(Award(leader, "orden-sv-yuriia-bronza", new DateTime(2018, 4, 23))),
            Award(leader, "usp-hramota-zasluhy", new DateTime(2015, 3, 14), "За вишкіл новаків"),
            Award(members[1], "usp-hramota-podiaky", new DateTime(2024, 6, 9)));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static readonly (string First, string Last, string Login, int Year)[] OrdenMembers =
    [
        ("Богдан", "Лисяк", "lysiak", 1995),
        ("Ірина", "Федорів", "fedoriv", 1998),
        ("Тарас", "Мельничук", "melnychuk", 1972),
        ("Олена", "Ничай", "nychai", 1969),
        ("Андрій", "Савчин", "savchyn", 2001)
    ];

    private async Task<Kurin> EnsureSeniorKurinAsync(
        IReadOnlyCollection<KurinNumbering> branches,
        string name,
        string stanytsia,
        string description,
        CancellationToken cancellationToken)
    {
        var main = branches.First();
        var kurin = await _dbContext.Kurins.FirstOrDefaultAsync(
            k => k.KraiCode == Krai.Ukraine && k.Branches.Any(b => b.Branch == main.Branch && b.Number == main.Number),
            cancellationToken);
        if (kurin is null)
        {
            kurin = Kurin.InBranches(branches);
            _dbContext.Kurins.Add(kurin);
        }

        kurin.NamedAfter = name;
        kurin.Stanytsia = stanytsia;
        kurin.RegionOrCountry = "Україна";
        kurin.Description = description;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return kurin;
    }

    /// <summary>The kurin's Зв'язковий: a КВ office, which is where the rights in its settings come from.</summary>
    private async Task<Member> EnsureSeniorLeaderAsync(Guid kurinKey, string email, string firstName, string lastName, int birthYear, CancellationToken cancellationToken)
    {
        var member = await DataSeeder.EnsureMemberAsync(
            _dbContext,
            _userManager,
            new DataSeeder.SeededPerson(email, firstName, null, lastName, kurinKey, null, NextPhone(), new DateOnly(birthYear, 5, 12), MembershipKind.Staff, _now.AddYears(-6)),
            cancellationToken);

        var kv = await EnsureLeadershipAsync(LeadershipType.KV, kurinKey, null, new DateOnly(2023, 9, 1), cancellationToken);
        SeatOffice(kv, member.MemberKey, LeadershipRole.Zvyazkovyi, new DateOnly(2023, 9, 1));
        return member;
    }

    private async Task<Member> EnsureSeniorMemberAsync(Guid kurinKey, string firstName, string lastName, string email, int birthYear, CancellationToken cancellationToken)
    {
        return await DataSeeder.EnsureMemberAsync(
            _dbContext,
            _userManager,
            new DataSeeder.SeededPerson(email, firstName, null, lastName, kurinKey, null, NextPhone(), new DateOnly(birthYear, 2, 3), MembershipKind.Youth, _now.AddYears(-4)),
            cancellationToken);
    }
}
