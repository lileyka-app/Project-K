using ProjectK.Common.Interfaces.Modules.DuesModule;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>
/// Opens a kurin's <see cref="DuesLedger"/> the way every read of the box does: charges brought up to
/// the current quarter first, then every rate, concession, charge and entry of the kurin read once.
/// </summary>
public sealed class DuesLedgerReader
{
    private readonly IDuesUnitOfWork _dues;
    private readonly IDuesAccrual _accrual;

    public DuesLedgerReader(IDuesUnitOfWork dues, IDuesAccrual accrual)
    {
        _dues = dues;
        _accrual = accrual;
    }

    public async Task<DuesLedger> OpenAsync(Guid kurinKey, CancellationToken cancellationToken)
    {
        await _accrual.AccrueKurinAsync(kurinKey, cancellationToken);

        return new DuesLedger(
            await _dues.KurinDuesRates.GetForKurinAsync(kurinKey, cancellationToken),
            await _dues.GroupDuesRates.GetForKurinAsync(kurinKey, cancellationToken),
            await _dues.DuesConcessions.GetForKurinAsync(kurinKey, cancellationToken),
            await _dues.DuesCharges.GetForKurinAsync(kurinKey, cancellationToken),
            await _dues.DuesEntries.GetForKurinAsync(kurinKey, cancellationToken),
            await _dues.DuesConcessionGrants.GetForKurinAsync(kurinKey, cancellationToken));
    }
}
