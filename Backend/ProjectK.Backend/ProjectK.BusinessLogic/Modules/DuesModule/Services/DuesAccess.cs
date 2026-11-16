using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.DuesModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>
/// The box every dues request is about, checked to stand in the kurin the caller acts in: a гурток,
/// or the kurin itself and its own operations by key. The endpoint's authorization already pins
/// both; this is the handlers' shared answer to "is it ours and does it exist", and it keeps the
/// kurin key out of every request body.
/// </summary>
public sealed class DuesAccess
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDuesUnitOfWork _dues;
    private readonly ICurrentUserContext _currentUser;
    private readonly IDuesKurinShape _shape;

    public DuesAccess(IUnitOfWork unitOfWork, IDuesUnitOfWork dues, ICurrentUserContext currentUser, IDuesKurinShape shape)
    {
        _unitOfWork = unitOfWork;
        _dues = dues;
        _currentUser = currentUser;
        _shape = shape;
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

    /// <summary>The гурток, when it stands in the kurin the caller acts in.</summary>
    public async Task<(Group? Group, ServiceResult<T>? Failure)> OpenGroupAsync<T>(Guid groupKey, CancellationToken cancellationToken)
    {
        var group = await _unitOfWork.Groups.GetByKeyAsync(groupKey, cancellationToken);
        if (group is null)
        {
            return (null, DuesErrors.NoGroup<T>());
        }

        if (_currentUser.KurinKey != group.KurinKey)
        {
            return (null, DuesErrors.Refused<T>());
        }

        return (group, null);
    }

    /// <summary>
    /// The box a person's account is kept in: a гурток by its key, or — given the key of the kurin the
    /// caller acts in, when that kurin has no гуртки — the kurin's own box.
    /// </summary>
    public async Task<(DuesBox? Box, ServiceResult<T>? Failure)> OpenBoxAsync<T>(Guid boxKey, CancellationToken cancellationToken)
    {
        if (boxKey == _currentUser.KurinKey)
        {
            return await _shape.IsSeniorAsync(boxKey, cancellationToken)
                ? (new DuesBox(boxKey, null), null)
                : (null, DuesErrors.NoGroup<T>());
        }

        var (group, failure) = await OpenGroupAsync<T>(boxKey, cancellationToken);
        return group is null ? (null, failure) : (new DuesBox(group.KurinKey, group.GroupKey), null);
    }
}
