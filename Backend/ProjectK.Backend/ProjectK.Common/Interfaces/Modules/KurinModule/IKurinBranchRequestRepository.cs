using ProjectK.Common.Entities.KurinModule;

namespace ProjectK.Common.Interfaces.Modules.KurinModule;

public interface IKurinBranchRequestRepository : IBaseEntityRepository<KurinBranchRequest>
{
    /// <summary>The request a kurin is waiting on, if it has one: a kurin keeps at most one open.</summary>
    Task<KurinBranchRequest?> GetPendingForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Settles the request only if it is still waiting, in one statement, so a parallel approve and
    /// reject cannot both go through. False when someone settled it first. Run it inside the
    /// transaction that applies the decision, so a later failure takes the settlement back too.
    /// </summary>
    Task<bool> TrySettleAsync(Guid requestKey, KurinBranchRequestStatus status, Guid? reviewedByUserKey, DateTime reviewedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Every request still waiting for an administrator, oldest first.</summary>
    Task<IReadOnlyList<KurinBranchRequest>> GetPendingAsync(CancellationToken cancellationToken = default);
}
