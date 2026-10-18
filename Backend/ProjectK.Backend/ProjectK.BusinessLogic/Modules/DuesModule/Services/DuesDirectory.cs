using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <inheritdoc />
/// <remarks>
/// A quarter is paid when the ledger leaves it with no balance owed — and the ledger pays the oldest
/// quarter first, so a quarter paid is one every quarter before it is paid too. Charges are brought
/// up to date first, as every read of the box does.
/// </remarks>
public sealed class DuesDirectory : IDuesDirectory
{
    private readonly DuesLedgerReader _ledgers;
    private readonly TimeProvider _time;
    private readonly IKraiProfileProvider _profiles;

    public DuesDirectory(DuesLedgerReader ledgers, TimeProvider time, IKraiProfileProvider profiles)
    {
        _ledgers = ledgers;
        _time = time;
        _profiles = profiles;
    }

    public async Task<IReadOnlyCollection<PaidQuarterRecord>> GetPaidQuartersAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var ledger = await _ledgers.OpenAsync(kurinKey, cancellationToken);

        var current = (await _profiles.DuesKraiForKurinAsync(kurinKey, cancellationToken)).QuarterOf(_time.GetUtcNow());
        return ledger.Accounts()
            .SelectMany(account => account.Quarters
                .Where(q => q.Quarter < current && q.Charged.Total > 0 && q.Balance >= 0)
                .Select(q => new PaidQuarterRecord(account.MembershipKey, q.Quarter.Index, q.Quarter.Next().FirstDay.AddDays(-1))))
            .Distinct()
            .ToList();
    }
}
