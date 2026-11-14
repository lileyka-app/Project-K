using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>
/// The kurin every kurin-box request is about, checked to be the one the caller acts in, and the
/// kurin's own operations by key. The endpoint's authorization already pins the kurin; this is the
/// handlers' shared answer to "is it ours and does it exist".
/// </summary>
public sealed class KurinDuesAccess
{
    private readonly IDuesUnitOfWork _dues;
    private readonly ICurrentUserContext _currentUser;

    public KurinDuesAccess(IDuesUnitOfWork dues, ICurrentUserContext currentUser)
    {
        _dues = dues;
        _currentUser = currentUser;
    }

    public ServiceResult<T>? Refuse<T>(Guid kurinKey) =>
        kurinKey == Guid.Empty || _currentUser.KurinKey != kurinKey ? DuesErrors.Refused<T>() : null;

    /// <summary>One of the kurin's own operations, tracked with its trail; null when it is not here.</summary>
    public async Task<DuesEntry?> OwnEntryAsync(Guid kurinKey, Guid entryKey, CancellationToken cancellationToken)
    {
        var entry = await _dues.DuesEntries.GetByKeyAsync(entryKey, cancellationToken);
        return entry is null || entry.KurinKey != kurinKey || entry.GroupKey is not null || entry.IsDeleted ? null : entry;
    }

    /// <summary>A гурток's transfer to this kurin, tracked with its trail; null when it is not one.</summary>
    public async Task<DuesEntry?> TransferAsync(Guid kurinKey, Guid entryKey, CancellationToken cancellationToken)
    {
        var entry = await _dues.DuesEntries.GetByKeyAsync(entryKey, cancellationToken);
        return entry is null || entry.KurinKey != kurinKey || entry.GroupKey is null
            || entry.Kind != DuesEntryKind.TransferToKurin || entry.IsDeleted ? null : entry;
    }
}
