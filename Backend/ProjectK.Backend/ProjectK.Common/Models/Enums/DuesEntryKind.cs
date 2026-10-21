namespace ProjectK.Common.Models.Enums;

/// <summary>
/// What a dues operation is. Stored as a number, so values are appended and never renumbered.
/// </summary>
public enum DuesEntryKind
{
    /// <summary>A person paid their вкладка. Counts towards their balance and into the box.</summary>
    Contribution = 0,

    /// <summary>An overpayment handed back. Out of the box and off the person's balance.</summary>
    Refund = 1,

    /// <summary>A group box hands money up to the курінний скарбник.</summary>
    TransferToKurin = 2,

    /// <summary>The kurin box hands money up to the станиця.</summary>
    TransferToStanytsia = 3,

    /// <summary>Spent from the box's own money: repairs, food, travel.</summary>
    Expense = 4,

    /// <summary>Money in that is not anyone's вкладка — a donation, a fundraiser.</summary>
    OtherIncome = 5,

    /// <summary>Cash taken to the card or the other way round; the total does not change.</summary>
    Exchange = 6,

    /// <summary>
    /// Fixes a person's balance without money moving — the way to correct an entry that is already
    /// verified. The only kind whose amount may be negative.
    /// </summary>
    Correction = 7,

    /// <summary>The kurin box hands the край its статті straight, not through the станиця.</summary>
    TransferToKrai = 8
}
