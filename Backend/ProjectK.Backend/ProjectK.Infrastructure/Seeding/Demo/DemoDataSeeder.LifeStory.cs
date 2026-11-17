using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.Infrastructure.Seeding.Demo;

/// <summary>A senior of the Order whose «Пластовий життєпис» goes back to kurin 1.</summary>
public partial class DemoDataSeeder
{
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
}
