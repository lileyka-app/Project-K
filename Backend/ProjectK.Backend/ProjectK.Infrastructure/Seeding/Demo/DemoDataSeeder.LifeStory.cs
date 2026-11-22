using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.Infrastructure.Seeding.Demo;

/// <summary>A senior of Сторожі Горган whose «Пластовий життєпис» goes back to kurin 1.</summary>
public partial class DemoDataSeeder
{
    internal const string LifeStoryEmail = "storozhi.kozak@projectk.com";

    /// <summary>
    /// A senior of Сторожі Горган who grew up in kurin 1: «Ведмеді» from 2015, the курінний провід's
    /// писар, two probes, вмілості and an УПЮ відзначення, then старший пластун in Сторожі Горган. His
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
        var storozhi = await _dbContext.Kurins.FirstAsync(k => k.NamedAfter == "Сторожі Горган", cancellationToken);
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
            KurinKey = storozhi.KurinKey,
            Kind = MembershipKind.Youth,
            JoinedAtUtc = new DateTime(2022, 9, 15, 0, 0, 0, DateTimeKind.Utc)
        });

        foreach (var (level, when, kurinKey) in new (PlastLevel, DateOnly, Guid?)[]
                 {
                     (PlastLevel.Entry, new DateOnly(2015, 9, 1), youthKurinKey),
                     (PlastLevel.Uchasnyk, new DateOnly(2016, 10, 8), youthKurinKey),
                     (PlastLevel.Rozviduvach, new DateOnly(2018, 6, 2), youthKurinKey),
                     (PlastLevel.Skob, new DateOnly(2020, 7, 18), youthKurinKey),
                     (PlastLevel.Starshoplastun, new DateOnly(2022, 10, 1), storozhi.KurinKey)
                 })
        {
            _dbContext.PlastLevelHistories.Add(new PlastLevelHistory { MemberKey = member.MemberKey, KurinKey = kurinKey, PlastLevel = level, DateAchieved = when });
        }

        member.LatestPlastLevel = PlastLevel.Starshoplastun;
        SeatOffice(youthKurinLeadership, member.MemberKey, LeadershipRole.Pysar, new DateOnly(2020, 9, 1), new DateOnly(2022, 6, 30));

        // His youth ended in 2022: each probe closes before the ступінь it leads to, and every вмілість
        // falls inside his years in kurin 1, not «a few days ago» like a current youth's.
        var person = new Person(member, vedmedi.GroupKey, joined, 22) { Level = PlastLevel.Skob };
        var probes = _catalog.GetProbes();
        if (probes.Count > 0)
        {
            SignPoints(person, probes[0], 1.0, mentor, youthKurinKey, ProbeProgressStatus.Verified, zvyazkovyi, (UtcDay(2016, 11, 5), UtcDay(2018, 5, 12)));
        }

        if (probes.Count > 1)
        {
            SignPoints(person, probes[1], 1.0, mentor, youthKurinKey, ProbeProgressStatus.Verified, zvyazkovyi, (UtcDay(2018, 9, 15), UtcDay(2020, 6, 27)));
        }

        var badgeIds = _catalog.GetBadgeIds();
        if (badgeIds.Count > 0)
        {
            SeedBadgesFor(person, badgeIds, mentor, youthKurinKey, awaitingReview: false, refused: false, (joined.AddDays(60), youth.LeftAtUtc.Value.AddDays(-30)));
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

        // And one of Сторожі Горган's, recorded by himself as senior відзначення are.
        var hramota = new DateTime(2024, 5, 18, 0, 0, 0, DateTimeKind.Utc);
        _dbContext.MemberAwards.Add(new MemberAward
        {
            MemberKey = member.MemberKey,
            KurinKey = storozhi.KurinKey,
            CatalogId = "usp-hramota-zasluhy",
            DateAcquired = hramota,
            Note = "За мандрівку Чорногорою",
            Status = BadgeProgressStatus.Confirmed,
            SubmittedAtUtc = hramota,
            SubmittedByUserKey = member.UserKey
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
