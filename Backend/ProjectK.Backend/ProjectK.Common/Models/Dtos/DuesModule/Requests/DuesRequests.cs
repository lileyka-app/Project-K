using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.DuesModule.Requests;

/// <summary>A calendar quarter as the client names it.</summary>
public sealed class QuarterRequest
{
    public int Year { get; set; }
    public int Number { get; set; }
}

/// <summary>The гурток's part of the вкладка, from a quarter on.</summary>
public sealed class SetGroupDuesRateRequest
{
    public QuarterRequest FromQuarter { get; set; } = new();
    public decimal GroupShare { get; set; }
}

/// <summary>
/// The kurin's статті of the вкладка, from a quarter on, for the whole kurin.
/// <para>
/// A client that does not know статті yet leaves <see cref="Articles"/> out and sends the three
/// amounts instead; they become a «Станиця» and a «Курінь» стаття.
/// </para>
/// </summary>
public sealed class SetKurinDuesRateRequest
{
    public QuarterRequest FromQuarter { get; set; } = new();
    public List<DuesArticleRequest>? Articles { get; set; }
    public decimal StanytsiaFull { get; set; }
    public decimal StanytsiaReduced { get; set; }
    public decimal KurinShare { get; set; }
}

/// <summary>One стаття of the вкладка: its name, where the money goes, and what it costs.</summary>
public sealed class DuesArticleRequest
{
    public string Name { get; set; } = string.Empty;
    public DuesRecipient Recipient { get; set; }
    public decimal Amount { get; set; }

    /// <summary>With a пільга. Left out, it is the full amount.</summary>
    public decimal? ReducedAmount { get; set; }
}

/// <summary>A пільга switched on or off for one membership, from a quarter on.</summary>
public sealed class SetDuesConcessionRequest
{
    public QuarterRequest FromQuarter { get; set; } = new();
    public bool IsConcession { get; set; }
}

/// <summary>One operation of a гурток's box, new or edited.</summary>
public sealed class UpsertDuesEntryRequest
{
    public DuesEntryKind Kind { get; set; }
    public DuesPaymentMethod Method { get; set; }
    public DuesPaymentMethod? CounterMethod { get; set; }
    public decimal Amount { get; set; }
    public DateOnly OccurredOn { get; set; }
    public Guid? MembershipKey { get; set; }
    public Guid? CollectedByMemberKey { get; set; }
    public string? Note { get; set; }
}

public sealed class SetDuesEntryVerifiedRequest
{
    public bool IsVerified { get; set; }
}

public sealed class SetDuesTransferReceivedRequest
{
    public bool IsReceived { get; set; }
}
