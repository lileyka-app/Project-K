using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.ProbesAndBadgesModule;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.AuthModule;

namespace ProjectK.Infrastructure.Seeding.Demo;

/// <summary>Probes signed point by point, and вмілості confirmed, refused or waiting for review.</summary>
public partial class DemoDataSeeder
{
    private static readonly string[] BadgeConfirmNotes =
    {
        "Показав на сходинах", "Здав на таборі", "Перевірив на мандрівці", "Підтверджено на вишколі", null!
    };

    private void SeedProgress(IReadOnlyList<Person> youth, IReadOnlyDictionary<Guid, Person> mentors, Person zvyazkovyi, Guid kurinKey)
    {
        var probes = _catalog.GetProbes();
        var badgeIds = _catalog.GetBadgeIds();

        // Five people whose вмілості sit in the review queue right now, one already refused.
        var awaitingReview = youth.Where((_, i) => i % 5 == 2).Take(5).Select(p => p.Key).ToHashSet();
        var refused = youth.Where(p => !awaitingReview.Contains(p.Key)).Skip(3).First().Key;

        foreach (var person in youth)
        {
            var mentor = mentors[person.GroupKey!.Value];

            if (probes.Count > 0)
            {
                SeedProbesFor(person, probes, mentor, zvyazkovyi, kurinKey);
            }

            if (badgeIds.Count > 0)
            {
                SeedBadgesFor(person, badgeIds, mentor, kurinKey, awaitingReview.Contains(person.Key), person.Key == refused);
            }
        }
    }

    private void SeedProbesFor(Person person, IReadOnlyList<CatalogProbe> probes, Person mentor, Person zvyazkovyi, Guid kurinKey)
    {
        switch (person.Level)
        {
            case PlastLevel.Entry or PlastLevel.Prykhylnyk:
                if (_random.NextDouble() < 0.4)
                {
                    return;
                }

                SignPoints(person, probes[0], Fraction(0.05, 0.35), mentor, kurinKey, ProbeProgressStatus.InProgress, null);
                break;

            case PlastLevel.Uchasnyk:
                SignPoints(person, probes[0], Fraction(0.3, 0.8), mentor, kurinKey, ProbeProgressStatus.InProgress, null);
                break;

            case PlastLevel.Rozviduvach:
                SignPoints(person, probes[0], 1.0, mentor, kurinKey, ProbeProgressStatus.Verified, zvyazkovyi);
                if (probes.Count > 1)
                {
                    SignPoints(person, probes[1], Fraction(0.15, 0.6), mentor, kurinKey, ProbeProgressStatus.InProgress, null);
                }

                break;

            case PlastLevel.Skob:
                SignPoints(person, probes[0], 1.0, mentor, kurinKey, ProbeProgressStatus.Verified, zvyazkovyi);
                if (probes.Count > 1)
                {
                    var secondStatus = _random.NextDouble() < 0.5 ? ProbeProgressStatus.Completed : ProbeProgressStatus.Verified;
                    SignPoints(person, probes[1], 1.0, mentor, kurinKey, secondStatus, zvyazkovyi);
                }

                if (probes.Count > 2)
                {
                    SignPoints(person, probes[2], Fraction(0.05, 0.25), mentor, kurinKey, ProbeProgressStatus.InProgress, null);
                }

                break;
        }
    }

    /// <summary>
    /// Signs the first <paramref name="fraction"/> of a probe's points the way the виховник would
    /// have over the months since the person joined, then moves the probe to
    /// <paramref name="status"/> with the same trail the status handler leaves.
    /// </summary>
    private void SignPoints(
        Person person,
        CatalogProbe probe,
        double fraction,
        Person mentor,
        Guid kurinKey,
        ProbeProgressStatus status,
        Person? verifier)
    {
        var total = probe.PointIds.Count;
        if (total == 0)
        {
            return;
        }

        var signedCount = status is ProbeProgressStatus.Completed or ProbeProgressStatus.Verified
            ? total
            : Math.Clamp((int)Math.Round(total * fraction), 1, Math.Max(1, total));

        var mentorRole = SystemRoleNames.ForOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk);
        var firstSignature = Later(person.JoinedAtUtc.AddDays(30), DaysAgo(540));
        var lastSignature = DaysAgo(_random.Next(3, 40));
        if (lastSignature <= firstSignature)
        {
            lastSignature = firstSignature.AddDays(1);
        }

        var span = (lastSignature - firstSignature).TotalDays;
        for (var i = 0; i < signedCount; i++)
        {
            var signedAt = firstSignature.AddDays(span * i / Math.Max(1, signedCount - 1)).AddMinutes(_random.Next(0, 600));
            _dbContext.ProbePointProgresses.Add(new ProbePointProgress
            {
                MemberKey = person.Key,
                KurinKey = kurinKey,
                ProbeId = probe.Id,
                PointId = probe.PointIds[i],
                IsSigned = true,
                SignedAtUtc = signedAt,
                SignedByUserKey = mentor.UserKey,
                SignedByName = mentor.FullName,
                SignedByRole = mentorRole
            });
        }

        var progress = new ProbeProgress
        {
            MemberKey = person.Key,
            KurinKey = kurinKey,
            ProbeId = probe.Id,
            Status = status
        };

        if (status is ProbeProgressStatus.Completed or ProbeProgressStatus.Verified)
        {
            var completedAt = lastSignature.AddDays(1);
            progress.CompletedAtUtc = completedAt;
            progress.CompletedByUserKey = mentor.UserKey;
            progress.CompletedByName = mentor.FullName;
            progress.CompletedByRole = mentorRole;
            AddJournalEntry(
                person, kurinKey, AuditArea.Probe, AuditActionNames.Probe.Completed, progress.ProbeProgressKey, probe.Title,
                ProbeProgressStatus.InProgress, ProbeProgressStatus.Completed, mentor, mentorRole, completedAt, "Усі точки підписані");
        }

        if (status == ProbeProgressStatus.Verified && verifier is not null)
        {
            var verifiedAt = Earlier(progress.CompletedAtUtc!.Value.AddDays(_random.Next(2, 14)), _now.AddHours(-1));
            var verifierRole = SystemRoleNames.ForOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi);
            progress.VerifiedAtUtc = verifiedAt;
            progress.VerifiedByUserKey = verifier.UserKey;
            progress.VerifiedByName = verifier.FullName;
            progress.VerifiedByRole = verifierRole;
            AddJournalEntry(
                person, kurinKey, AuditArea.Probe, AuditActionNames.Probe.Verified, progress.ProbeProgressKey, probe.Title,
                ProbeProgressStatus.Completed, ProbeProgressStatus.Verified, verifier, verifierRole, verifiedAt, "Перевірено на сходинах куреня");
        }

        _dbContext.ProbeProgresses.Add(progress);
    }

    private void SeedBadgesFor(
        Person person,
        IReadOnlyList<string> badgeIds,
        Person mentor,
        Guid kurinKey,
        bool awaitingReview,
        bool refused)
    {
        var confirmedCount = person.Level switch
        {
            PlastLevel.Skob => _random.Next(3, 6),
            PlastLevel.Rozviduvach => _random.Next(2, 4),
            PlastLevel.Uchasnyk => _random.Next(0, 3),
            _ => _random.Next(0, 2)
        };

        var taken = new HashSet<string>();
        var mentorRole = SystemRoleNames.ForOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk);

        for (var i = 0; i < confirmedCount; i++)
        {
            var badgeId = PickDistinct(badgeIds, taken);
            if (badgeId is null)
            {
                break;
            }

            var submittedAt = Later(person.JoinedAtUtc.AddDays(60), DaysAgo(_random.Next(30, 400)));
            var reviewedAt = submittedAt.AddDays(_random.Next(1, 10));
            var note = Pick(BadgeConfirmNotes);
            var progress = new BadgeProgress
            {
                MemberKey = person.Key,
                KurinKey = kurinKey,
                BadgeId = badgeId,
                Status = BadgeProgressStatus.Confirmed,
                SubmittedAtUtc = submittedAt,
                ReviewedAtUtc = reviewedAt,
                ReviewedByUserKey = mentor.UserKey,
                ReviewedByName = mentor.FullName,
                ReviewedByRole = mentorRole,
                ReviewNote = note
            };
            AddSubmittedEntry(progress, person, submittedAt);
            AddJournalEntry(
                person, kurinKey, AuditArea.Badge, AuditActionNames.Badge.Confirmed, progress.BadgeProgressKey, _catalog.GetBadgeTitle(badgeId),
                BadgeProgressStatus.Submitted, BadgeProgressStatus.Confirmed, mentor, mentorRole, reviewedAt, note);
            _dbContext.BadgeProgresses.Add(progress);
        }

        if (awaitingReview)
        {
            var badgeId = PickDistinct(badgeIds, taken);
            if (badgeId is not null)
            {
                var submittedAt = DaysAgo(_random.Next(1, 9)).AddHours(-_random.Next(0, 20));
                var progress = new BadgeProgress
                {
                    MemberKey = person.Key,
                    KurinKey = kurinKey,
                    BadgeId = badgeId,
                    Status = BadgeProgressStatus.Submitted,
                    SubmittedAtUtc = submittedAt,
                    ReviewNote = "Здав на останніх сходинах, прошу підтвердити"
                };
                AddSubmittedEntry(progress, person, submittedAt, progress.ReviewNote);
                _dbContext.BadgeProgresses.Add(progress);
            }
        }

        if (refused)
        {
            var badgeId = PickDistinct(badgeIds, taken);
            if (badgeId is not null)
            {
                var submittedAt = DaysAgo(21);
                var reviewedAt = DaysAgo(18);
                const string note = "Бракує практичної частини — спробуй ще раз після табору";
                var progress = new BadgeProgress
                {
                    MemberKey = person.Key,
                    KurinKey = kurinKey,
                    BadgeId = badgeId,
                    Status = BadgeProgressStatus.Rejected,
                    SubmittedAtUtc = submittedAt,
                    ReviewedAtUtc = reviewedAt,
                    ReviewedByUserKey = mentor.UserKey,
                    ReviewedByName = mentor.FullName,
                    ReviewedByRole = mentorRole,
                    ReviewNote = note
                };
                AddSubmittedEntry(progress, person, submittedAt);
                AddJournalEntry(
                    person, kurinKey, AuditArea.Badge, AuditActionNames.Badge.Rejected, progress.BadgeProgressKey, _catalog.GetBadgeTitle(badgeId),
                    BadgeProgressStatus.Submitted, BadgeProgressStatus.Rejected, mentor, mentorRole, reviewedAt, note);
                _dbContext.BadgeProgresses.Add(progress);
            }
        }
    }

    private void AddSubmittedEntry(BadgeProgress progress, Person person, DateTime submittedAt, string? note = null) =>
        AddJournalEntry(
            person, progress.KurinKey, AuditArea.Badge, AuditActionNames.Badge.Submitted, progress.BadgeProgressKey, _catalog.GetBadgeTitle(progress.BadgeId),
            null, BadgeProgressStatus.Submitted, person, SystemRoleNames.Member, submittedAt, note);
}
