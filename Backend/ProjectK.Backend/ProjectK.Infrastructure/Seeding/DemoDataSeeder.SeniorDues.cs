using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.KurinModule.Tags;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Infrastructure.Seeding;

/// <summary>
/// The Order's вкладка: no гуртки, so each person pays into the kurin's own box. Tags УСП and УПС
/// set the rates, their статті go to the край and the kurin, and its people show every state the
/// register has — paid a year ahead, paid up, owing for past quarters, on a пільга. Богдан Лисяк
/// keeps the box as скарбник. The demo край takes вкладка both through the станиця and straight,
/// so the Order can hand the край its part.
/// </summary>
public partial class DemoDataSeeder
{
    private static readonly string[] UspMembers = ["lysiak", "fedoriv", "savchyn"];

    private async Task SeedSeniorDuesAsync(Kurin orden, Member leader, IReadOnlyList<Member> members, Leadership leadership, CancellationToken cancellationToken)
    {
        await LetTheDemoKraiTakeDuesStraightAsync(cancellationToken);
        if (await _dbContext.KurinDuesRates.AnyAsync(r => r.KurinKey == orden.KurinKey, cancellationToken))
        {
            return;
        }

        var treasurer = members[0];
        SeatOffice(leadership, treasurer.MemberKey, LeadershipRole.Skarbnyk, new DateOnly(2025, 9, 1));

        var usp = new KurinTag { KurinKey = orden.KurinKey, Name = "УСП", ColorHex = "#2F855A" };
        var ups = new KurinTag { KurinKey = orden.KurinKey, Name = "УПС", ColorHex = "#2B6CB0" };
        _dbContext.KurinTags.AddRange(usp, ups);
        foreach (var (member, login) in members.Zip(OrdenMembers.Select(m => m.Login)))
        {
            _dbContext.MemberTags.Add(new MemberTag
            {
                KurinTagKey = UspMembers.Contains(login) ? usp.KurinTagKey : ups.KurinTagKey,
                MemberKey = member.MemberKey
            });
        }

        var today = DateOnly.FromDateTime(_now);
        var current = DuesQuarter.Of(today);
        var first = DuesQuarter.FromIndex(current.Index - 3);
        KurinDuesRate Rate(Guid? tagKey, params KurinDuesArticle[] articles)
        {
            var rate = new KurinDuesRate { KurinKey = orden.KurinKey, TagKey = tagKey, FromQuarter = first.Index, SetByUserKey = leader.UserKey };
            rate.SetArticles(articles);
            return rate;
        }

        _dbContext.KurinDuesRates.AddRange(
            Rate(null, new KurinDuesArticle { Name = "Членська вкладка", Recipient = DuesRecipient.Kurin, Amount = 400, ReducedAmount = 300 }),
            Rate(usp.KurinTagKey,
                new KurinDuesArticle { Name = "Вкладка УСП", Recipient = DuesRecipient.Krai, Amount = 300, ReducedAmount = 200 },
                new KurinDuesArticle { Name = "Курінна", Recipient = DuesRecipient.Kurin, Amount = 150, ReducedAmount = 100 }),
            Rate(ups.KurinTagKey,
                new KurinDuesArticle { Name = "Вкладка УПС", Recipient = DuesRecipient.Krai, Amount = 250, ReducedAmount = 125 },
                new KurinDuesArticle { Name = "Курінна", Recipient = DuesRecipient.Kurin, Amount = 100, ReducedAmount = 50 }));

        var memberKeys = members.Select(m => m.MemberKey).ToList();
        var memberships = await _dbContext.Memberships
            .Where(m => m.KurinKey == orden.KurinKey && memberKeys.Contains(m.MemberKey))
            .ToDictionaryAsync(m => m.MemberKey, m => m.MembershipKey, cancellationToken);

        // Ірина pays the пільгові amounts: 300 a quarter instead of 450.
        _dbContext.DuesConcessions.Add(new DuesConcession
        {
            KurinKey = orden.KurinKey,
            MembershipKey = memberships[members[1].MemberKey],
            FromQuarter = first.Index,
            IsConcession = true,
            SetByUserKey = leader.UserKey
        });

        var keeper = new Person(treasurer, null, _now.AddYears(-4), 31);
        var verifier = new Person(leader, null, _now.AddYears(-6), 47);
        void Pay(int member, decimal amount, DuesQuarter quarter, DuesPaymentMethod method, string? note = null) =>
            AddDuesEntry(orden.KurinKey, null, memberships[members[member].MemberKey], DuesEntryKind.Contribution, method,
                amount, PaymentDay(quarter, today), keeper, note, today, verifier);

        // Богдан: the year paid in the first quarter, and the next year ahead now.
        Pay(0, 1800, first, DuesPaymentMethod.Card, "За рік");
        Pay(0, 1800, current, DuesPaymentMethod.Card, "Наперед на наступний рік");
        // Ірина: two quarters of four, on the пільга.
        Pay(1, 300, first, DuesPaymentMethod.Cash);
        Pay(1, 300, DuesQuarter.FromIndex(first.Index + 1), DuesPaymentMethod.Cash);
        // Тарас: paid up, quarter by quarter.
        for (var i = 0; i < 4; i++)
        {
            Pay(2, 350, DuesQuarter.FromIndex(first.Index + i), DuesPaymentMethod.Card);
        }

        // Олена: one quarter, then nothing.
        Pay(3, 350, first, DuesPaymentMethod.Cash);
        // Андрій: three quarters; the running one is still to pay.
        Pay(4, 1350, DuesQuarter.FromIndex(first.Index + 2), DuesPaymentMethod.Card);

        void Hand(DuesEntryKind kind, decimal amount, DuesQuarter quarter, string note) =>
            AddDuesEntry(orden.KurinKey, null, null, kind, DuesPaymentMethod.Card, amount, PaymentDay(quarter, today), keeper, note, today, verifier);

        Hand(DuesEntryKind.TransferToKrai, 1700, DuesQuarter.FromIndex(first.Index + 2), "За І–ІІ кв.");
        Hand(DuesEntryKind.Expense, 300, DuesQuarter.FromIndex(first.Index + 1), "Оренда домівки");
    }

    /// <summary>
    /// The demo край takes вкладка both ways, so a kurin can send its край статті straight. Saved as a
    /// revision of the край's profile, the way the край's settings page would, and only while the
    /// profile is still empty.
    /// </summary>
    private async Task LetTheDemoKraiTakeDuesStraightAsync(CancellationToken cancellationToken)
    {
        var latest = await _dbContext.KraiProfileRevisions
            .Where(r => r.KraiCode == Krai.Ukraine)
            .OrderByDescending(r => r.Revision)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest is not null && latest.Json.Trim() != "{}")
        {
            return;
        }

        _dbContext.KraiProfileRevisions.Add(new KraiProfileRevision
        {
            KraiCode = Krai.Ukraine,
            Revision = (latest?.Revision ?? 0) + 1,
            Json = """{"dues":{"upstream":"both"}}""",
            Note = "Демо: курені передають і станиці, і краю"
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
