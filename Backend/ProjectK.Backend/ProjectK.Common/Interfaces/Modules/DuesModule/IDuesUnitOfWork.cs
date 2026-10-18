namespace ProjectK.Common.Interfaces.Modules.DuesModule;

/// <summary>
/// The dues module's way to its own tables. Like <see cref="IMemberUnitOfWork"/>, it is kept off
/// <see cref="IUnitOfWork"/> so no other module can reach money; the same instance backs both, so a
/// dues write still commits with the rest of the request.
/// </summary>
public interface IDuesUnitOfWork
{
    IKurinDuesRateRepository KurinDuesRates { get; }
    IGroupDuesRateRepository GroupDuesRates { get; }
    IDuesConcessionRepository DuesConcessions { get; }
    IDuesConcessionGrantRepository DuesConcessionGrants { get; }
    IDuesChargeRepository DuesCharges { get; }
    IDuesEntryRepository DuesEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken token = default);
}
