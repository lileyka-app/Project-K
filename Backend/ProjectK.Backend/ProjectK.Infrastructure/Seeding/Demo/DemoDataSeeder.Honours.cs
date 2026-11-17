using System.Text.Json;
using ProjectK.Common.Dtos.AuditModule;
using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.Infrastructure.Seeding.Demo;

/// <summary>Відзначення and перестороги, and the policy that keeps their senior levels with the Зв'язковий.</summary>
public partial class DemoDataSeeder
{
    private void SeedAwards(IReadOnlyList<Person> youth, IReadOnlyDictionary<Guid, Person> mentors, Person zvyazkovyi, Guid kurinKey)
    {
        var senior = youth.Where(p => p.Level is PlastLevel.Skob or PlastLevel.Rozviduvach).ToList();
        if (senior.Count == 0)
        {
            return;
        }

        var lastSummer = new DateTime(_now.Month >= 8 ? _now.Year : _now.Year - 1, 7, 20, 0, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < Math.Min(3, senior.Count); i++)
        {
            var mentor = mentors[senior[i].GroupKey!.Value];
            var award = new MemberAward
            {
                MemberKey = senior[i].Key,
                KurinKey = kurinKey,
                Level = MemberAwardLevel.First,
                DateAcquired = lastSummer,
                Note = "За участь у таборі «Соколине гніздо»",
                Status = BadgeProgressStatus.Confirmed,
                SubmittedAtUtc = lastSummer.AddDays(3),
                SubmittedByUserKey = mentor.UserKey,
                ReviewedAtUtc = lastSummer.AddDays(5),
                ReviewedByUserKey = zvyazkovyi.UserKey
            };
            _dbContext.MemberAwards.Add(award);
            AddAwardEntries(award, senior[i], mentor, VykhovnykRole, zvyazkovyi);
        }

        if (senior.Count > 3)
        {
            var award = new MemberAward
            {
                MemberKey = senior[3].Key,
                KurinKey = kurinKey,
                Level = MemberAwardLevel.Second,
                DateAcquired = DaysAgo(200),
                Note = "За проведення вишколу для молодших гуртків",
                Status = BadgeProgressStatus.Confirmed,
                SubmittedAtUtc = DaysAgo(198),
                SubmittedByUserKey = zvyazkovyi.UserKey,
                ReviewedAtUtc = DaysAgo(197),
                ReviewedByUserKey = zvyazkovyi.UserKey
            };
            _dbContext.MemberAwards.Add(award);
            AddAwardEntries(award, senior[3], zvyazkovyi, ZvyazkovyiRole, zvyazkovyi);
        }

        // One still on the Зв'язковий's desk.
        var pending = senior[^1];
        var pendingMentor = mentors[pending.GroupKey!.Value];
        var pendingAward = new MemberAward
        {
            MemberKey = pending.Key,
            KurinKey = kurinKey,
            Level = MemberAwardLevel.First,
            DateAcquired = DaysAgo(6),
            Note = "За організацію прибирання домівки",
            Status = BadgeProgressStatus.Submitted,
            SubmittedAtUtc = DaysAgo(4),
            SubmittedByUserKey = pendingMentor.UserKey
        };
        _dbContext.MemberAwards.Add(pendingAward);
        AddAwardEntries(pendingAward, pending, pendingMentor, VykhovnykRole, reviewer: null);
    }

    private void SeedWarnings(IReadOnlyList<Person> youth, IReadOnlyDictionary<Guid, Person> mentors, Person zvyazkovyi, Guid kurinKey)
    {
        if (youth.Count < 6)
        {
            return;
        }

        var active1 = youth[1];
        var active1Mentor = mentors[active1.GroupKey!.Value];
        var first = new MemberWarning
        {
            MemberKey = active1.Key,
            KurinKey = kurinKey,
            Level = MemberWarningLevel.Level1,
            IssuedAtUtc = DaysAgo(20),
            ExpiresAtUtc = DaysAgo(20).AddMonths(3),
            IssuedByUserKey = active1Mentor.UserKey
        };
        _dbContext.MemberWarnings.Add(first);
        AddWarningEntries(first, active1, active1Mentor, VykhovnykRole, revoker: null);

        var active2 = youth[4];
        var second = new MemberWarning
        {
            MemberKey = active2.Key,
            KurinKey = kurinKey,
            Level = MemberWarningLevel.Level2,
            IssuedAtUtc = DaysAgo(33),
            ExpiresAtUtc = DaysAgo(33).AddMonths(6),
            IssuedByUserKey = zvyazkovyi.UserKey
        };
        _dbContext.MemberWarnings.Add(second);
        AddWarningEntries(second, active2, zvyazkovyi, ZvyazkovyiRole, revoker: null);

        var revoked = youth[5];
        var revokedMentor = mentors[revoked.GroupKey!.Value];
        var cancelled = new MemberWarning
        {
            MemberKey = revoked.Key,
            KurinKey = kurinKey,
            Level = MemberWarningLevel.Level1,
            IssuedAtUtc = DaysAgo(150),
            ExpiresAtUtc = DaysAgo(150).AddMonths(3),
            IssuedByUserKey = revokedMentor.UserKey,
            RevokedAtUtc = DaysAgo(70),
            RevokedByUserKey = zvyazkovyi.UserKey
        };
        _dbContext.MemberWarnings.Add(cancelled);
        AddWarningEntries(cancelled, revoked, revokedMentor, VykhovnykRole, zvyazkovyi);
    }

    /// <summary>
    /// The demo kurin follows the правильник on the senior levels: only the Зв'язковий gives the third
    /// пересторога and confirms the third and fourth відзначення. The settings page shows these cells
    /// as changed, and the journal has the change that made them so.
    /// </summary>
    private void SeedAccessPolicy(Kurin kurin, Person zvyazkovyi)
    {
        (KurinCapability Capability, PolicyAudience Audience)[] narrowed =
        [
            (KurinCapability.GiveWarning3, PolicyAudience.Vykhovnyk),
            (KurinCapability.GiveWarning3, PolicyAudience.Instruktor),
            (KurinCapability.ConfirmAward3, PolicyAudience.Vykhovnyk),
            (KurinCapability.ConfirmAward3, PolicyAudience.Instruktor),
            (KurinCapability.ConfirmAward4, PolicyAudience.Vykhovnyk),
            (KurinCapability.ConfirmAward4, PolicyAudience.Instruktor)
        ];

        var grants = narrowed
            .GroupBy(cell => cell.Capability)
            .ToDictionary(
                row => row.Key.ToString(),
                row => row.ToDictionary(cell => cell.Audience.ToString(), _ => CapabilityReach.None.ToString()));
        kurin.AccessPolicy = JsonSerializer.Serialize(new { v = 1, grants });

        _dbContext.KurinAuditEvents.Add(new KurinAuditEvent
        {
            KurinKey = kurin.KurinKey,
            OccurredAtUtc = DaysAgo(40),
            ActorUserKey = zvyazkovyi.UserKey,
            ActorName = zvyazkovyi.FullName,
            ActorRole = ZvyazkovyiRole,
            Area = AuditArea.Settings,
            Action = AuditActionNames.Settings.AccessPolicyChanged,
            SubjectKey = kurin.KurinKey,
            SubjectLabel = "Права й видимість",
            Changes = narrowed
                .Select(cell => new AuditChange($"{cell.Capability}/{cell.Audience}", CapabilityReach.OwnGroup.ToString(), CapabilityReach.None.ToString()))
                .ToList(),
            Note = "Старші рівні — за рішенням ради куреня"
        });
    }
}
