using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Interfaces.Modules.DuesModule;

/// <summary>
/// What the dues module will tell others about who has paid. The whole of the way in: nobody
/// outside reads a box or a balance, and a quarter counts as paid only by the ledger's own rules.
/// </summary>
public interface IDuesDirectory
{
    /// <summary>
    /// Every quarter already over that a membership of the kurin closed with nothing owed — paid in
    /// full by the end of it, surplus or not. The running quarter is never among them.
    /// </summary>
    Task<IReadOnlyCollection<PaidQuarterRecord>> GetPaidQuartersAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// What the given memberships of the kurin still owe it for quarters already behind them, all
    /// their accounts together. The running quarter is left out whichever way the kurin pays: unpaid,
    /// its charge is dropped when they leave (see <c>DuesLeavingQuarterEventHandler</c>), so it is not a debt that
    /// keeps them. Zero when they are square.
    /// </summary>
    Task<decimal> GetOwedAsync(Guid kurinKey, IReadOnlyCollection<Guid> membershipKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether these memberships of the kurin carry anything in the ledger: a charge, a payment, a
    /// surplus. Such a membership is not deleted from the archive: its money would stay under a key
    /// nobody has any more.
    /// </summary>
    Task<bool> HasLedgerAsync(Guid kurinKey, IReadOnlyCollection<Guid> membershipKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a гурток's box is square: no money in it, nothing still owed up to the kurin, and no
    /// account of the гурток in debt. Only then may the гурток be dissolved — after that its box is
    /// no longer read.
    /// </summary>
    Task<bool> GroupBoxSettledAsync(Guid kurinKey, Guid groupKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves every unpaid quarter of the kurin's people to the rate their tags give now. Called once a
    /// tag is deleted: the running quarter follows on the next read, older ones would stay on a rate
    /// nobody can see or change. Paid quarters and the deleted tag's rates stay, so history reads the same.
    /// </summary>
    Task RepriceUnpaidAsync(Guid kurinKey, CancellationToken cancellationToken = default);
}
