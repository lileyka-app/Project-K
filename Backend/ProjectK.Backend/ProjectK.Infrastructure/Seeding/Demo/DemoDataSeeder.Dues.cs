using System.Text.Json;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.DuesModule;
using ProjectK.Common.Records.DuesModule;

namespace ProjectK.Infrastructure.Seeding.Demo;

/// <summary>A year of вкладка in kurin 1.</summary>
public partial class DemoDataSeeder
{
    /// <summary>
    /// A year of вкладка: статті from three quarters back, a пільга, payments that leave most
    /// accounts clear and a few in debt, гуртки handing their boxes up and the kurin paying the
    /// станиця. Charges are not written: the app accrues them on the first read.
    /// </summary>
    private void SeedDues(
        Guid kurinKey,
        IReadOnlyDictionary<string, Group> groups,
        IReadOnlyList<Person> youth,
        IReadOnlyDictionary<Guid, Person> mentors,
        Person zvyazkovyi,
        IReadOnlyDictionary<Guid, Guid> memberships)
    {
        var today = DateOnly.FromDateTime(_now);
        var current = DuesQuarter.Of(today);
        var first = DuesQuarter.FromIndex(current.Index - 3);

        var rate = new KurinDuesRate { KurinKey = kurinKey, FromQuarter = first.Index, SetByUserKey = zvyazkovyi.UserKey };
        rate.SetArticles(new[]
        {
            new KurinDuesArticle { Name = "Членський внесок станиці", Recipient = DuesRecipient.Stanytsia, Amount = 120, ReducedAmount = 60 },
            new KurinDuesArticle { Name = "Крайовий збір", Recipient = DuesRecipient.Stanytsia, Amount = 30, ReducedAmount = 15 },
            new KurinDuesArticle { Name = "Курінна каса", Recipient = DuesRecipient.Kurin, Amount = 50, ReducedAmount = 25 }
        });
        _dbContext.KurinDuesRates.Add(rate);

        var groupShares = new Dictionary<Guid, decimal>
        {
            [groups[Sokoly].GroupKey] = 30,
            [groups["Леви"].GroupKey] = 20,
            [groups["Ведмеді"].GroupKey] = 30
        };
        foreach (var (groupKey, share) in groupShares)
        {
            _dbContext.GroupDuesRates.Add(new GroupDuesRate
            {
                KurinKey = kurinKey,
                GroupKey = groupKey,
                FromQuarter = first.Index,
                GroupShare = share,
                SetByUserKey = mentors[groupKey].UserKey
            });
        }

        var reduced = youth.Where(p => p.GroupKey == groups["Леви"].GroupKey).Skip(2).First();
        _dbContext.DuesConcessions.Add(new DuesConcession
        {
            KurinKey = kurinKey,
            MembershipKey = memberships[reduced.Key],
            FromQuarter = first.Index,
            IsConcession = true,
            SetByUserKey = zvyazkovyi.UserKey
        });

        // What each гурток collected per quarter, in cash and by card, and how much of it is the
        // станиця's and the kurin's: the handovers below pass exactly that up, in the same money it
        // came in, so no box ever shows more sent than it held.
        var upward = new Dictionary<(Guid Group, int Quarter), Collected>();

        foreach (var person in youth)
        {
            var groupKey = person.GroupKey!.Value;
            var collector = mentors[groupKey];
            var isReduced = person == reduced;
            var stanytsia = isReduced ? rate.StanytsiaReduced : rate.StanytsiaFull;
            var own = isReduced ? rate.KurinReduced ?? rate.KurinShare : rate.KurinShare;
            var perQuarter = stanytsia + own + groupShares[groupKey];

            var quartersPaid = QuartersPaid(_random.NextDouble());
            for (var i = 0; i < quartersPaid; i++)
            {
                var quarter = DuesQuarter.FromIndex(first.Index + i);
                var byCard = _random.NextDouble() >= 0.7;
                AddDuesEntry(kurinKey, groupKey, memberships[person.Key], DuesEntryKind.Contribution,
                    byCard ? DuesPaymentMethod.Card : DuesPaymentMethod.Cash,
                    perQuarter, PaymentDay(quarter, today), collector, note: null, today, zvyazkovyi);

                var key = (groupKey, quarter.Index);
                var sum = upward.GetValueOrDefault(key) ?? new Collected();
                sum.Stanytsia += stanytsia;
                sum.Up += stanytsia + own;
                sum.Card += byCard ? perQuarter : 0;
                upward[key] = sum;
            }

            // Some of those a quarter behind have paid part of it.
            if (quartersPaid == 3 && _random.NextDouble() < 0.4)
            {
                AddDuesEntry(kurinKey, groupKey, memberships[person.Key], DuesEntryKind.Contribution, DuesPaymentMethod.Cash,
                    100, PaymentDay(current, today), collector, note: "Решту — на наступних сходинах", today, zvyazkovyi);
            }
        }

        decimal kurinCash = 0, kurinCard = 0;
        for (var index = first.Index; index < current.Index; index++)
        {
            var quarter = DuesQuarter.FromIndex(index);
            foreach (var groupKey in groupShares.Keys)
            {
                if (upward.TryGetValue((groupKey, index), out var sum))
                {
                    var (cash, card) = HandOver(sum.Up, sum.Card);
                    var sentOn = quarter.FirstDay.AddDays(50);
                    if (card > 0)
                    {
                        AddDuesEntry(kurinKey, groupKey, null, DuesEntryKind.TransferToKurin, DuesPaymentMethod.Card,
                            card, sentOn, mentors[groupKey], note: null, today, zvyazkovyi, received: true);
                    }

                    if (cash > 0)
                    {
                        AddDuesEntry(kurinKey, groupKey, null, DuesEntryKind.TransferToKurin, DuesPaymentMethod.Cash,
                            cash, sentOn, mentors[groupKey], note: null, today, zvyazkovyi, received: true);
                    }

                    kurinCash += cash;
                    kurinCard += card;
                }
            }

            // The станиця has been paid for every quarter but the last closed one.
            var due = upward.Where(u => u.Key.Quarter == index).Sum(u => u.Value.Stanytsia);
            if (index < current.Index - 1 && due > 0)
            {
                var byCard = Math.Min(due, kurinCard);
                kurinCard -= byCard;
                kurinCash -= due - byCard;
                var note = $"Станиці за {quarter.Number} квартал {quarter.Year}";
                if (byCard > 0)
                {
                    AddDuesEntry(kurinKey, null, null, DuesEntryKind.TransferToStanytsia, DuesPaymentMethod.Card,
                        byCard, quarter.FirstDay.AddDays(70), zvyazkovyi, note, today, zvyazkovyi);
                }

                if (due - byCard > 0)
                {
                    AddDuesEntry(kurinKey, null, null, DuesEntryKind.TransferToStanytsia, DuesPaymentMethod.Cash,
                        due - byCard, quarter.FirstDay.AddDays(70), zvyazkovyi, note, today, zvyazkovyi);
                }
            }
        }

        // Соколи have already handed this quarter's cash over; the kurin has not counted it yet.
        var sokoly = groups[Sokoly].GroupKey;
        if (upward.TryGetValue((sokoly, current.Index), out var sokolyNow) && HandOver(sokolyNow.Up, sokolyNow.Card).Cash > 0)
        {
            AddDuesEntry(kurinKey, sokoly, null, DuesEntryKind.TransferToKurin, DuesPaymentMethod.Cash,
                HandOver(sokolyNow.Up, sokolyNow.Card).Cash, today.AddDays(-2), mentors[sokoly],
                note: "Передав на сходинах куреня", today, zvyazkovyi);
        }
    }

    /// <summary>What a гурток collected in a quarter, as far as its handover is concerned.</summary>
    private sealed class Collected
    {
        public decimal Stanytsia { get; set; }

        /// <summary>The станиця's and the kurin's parts together: what goes up.</summary>
        public decimal Up { get; set; }

        /// <summary>Everything that came in by card, the гурток's own part included.</summary>
        public decimal Card { get; set; }
    }

    /// <summary>The card money goes up first, the rest in cash: the гурток keeps its own part.</summary>
    private static (decimal Cash, decimal Card) HandOver(decimal up, decimal collectedByCard)
    {
        var card = Math.Min(up, collectedByCard);
        return (up - card, card);
    }

    /// <summary>Of the four quarters charged so far: most paid all, some are one or more behind.</summary>
    private static int QuartersPaid(double habit)
    {
        if (habit < 0.55)
        {
            return 4;
        }

        if (habit < 0.8)
        {
            return 3;
        }

        return habit < 0.93 ? 2 : 1;
    }

    /// <summary>A day early in the quarter, never later than today.</summary>
    private DateOnly PaymentDay(DuesQuarter quarter, DateOnly today)
    {
        var day = quarter.FirstDay.AddDays(_random.Next(2, 40));
        return day > today ? today.AddDays(-_random.Next(0, Math.Max(1, today.DayNumber - quarter.FirstDay.DayNumber))) : day;
    }

    /// <summary>
    /// Writes one operation with the trail the app leaves for it: created by whoever collected it,
    /// verified by the Звʼязковий once it is a fortnight old, a handover received a few days after.
    /// </summary>
    private void AddDuesEntry(
        Guid kurinKey,
        Guid? groupKey,
        Guid? membershipKey,
        DuesEntryKind kind,
        DuesPaymentMethod method,
        decimal amount,
        DateOnly occurredOn,
        Person author,
        string? note,
        DateOnly today,
        Person verifier,
        bool received = false)
    {
        var createdAt = ToUtc(occurredOn.ToDateTime(TimeOnly.MinValue), 19);
        var entry = new DuesEntry
        {
            KurinKey = kurinKey,
            GroupKey = groupKey,
            MembershipKey = membershipKey,
            Kind = kind,
            Method = method,
            Amount = amount,
            OccurredOn = occurredOn,
            CollectedByMemberKey = kind == DuesEntryKind.Contribution ? author.Key : null,
            Note = note,
            CreatedByUserKey = author.UserKey,
            CreatedDate = createdAt
        };
        RecordDues(entry, "Created", author.UserKey, createdAt);

        if (received)
        {
            entry.ReceivedAtUtc = createdAt.AddDays(3);
            entry.ReceivedByUserKey = verifier.UserKey;
            RecordDues(entry, "Received", verifier.UserKey, entry.ReceivedAtUtc.Value);
        }

        if (today.DayNumber - occurredOn.DayNumber > 14)
        {
            entry.VerifiedAtUtc = createdAt.AddDays(_random.Next(4, 10));
            entry.VerifiedByUserKey = verifier.UserKey;
            RecordDues(entry, "Verified", verifier.UserKey, entry.VerifiedAtUtc.Value);
        }

        _dbContext.DuesEntries.Add(entry);
    }

    /// <summary>The same event <c>DuesEntryTrail</c> writes, which this layer cannot reach.</summary>
    private static void RecordDues(DuesEntry entry, string action, Guid? actorUserKey, DateTime atUtc)
    {
        entry.UpdatedDate = atUtc;
        entry.Events.Add(new DuesEntryEvent
        {
            DuesEntryKey = entry.DuesEntryKey,
            Action = action,
            ActorUserKey = actorUserKey,
            OccurredAtUtc = atUtc,
            CreatedDate = atUtc,
            UpdatedDate = atUtc,
            Snapshot = JsonSerializer.Serialize(new
            {
                entry.Kind,
                entry.Method,
                entry.CounterMethod,
                entry.Amount,
                entry.OccurredOn,
                entry.MembershipKey,
                entry.CollectedByMemberKey,
                entry.Note,
                entry.VerifiedAtUtc,
                entry.VerifiedByUserKey,
                entry.ReceivedAtUtc,
                entry.ReceivedByUserKey,
                entry.DeletedAtUtc,
                entry.DeletedByUserKey
            })
        });
    }
}
