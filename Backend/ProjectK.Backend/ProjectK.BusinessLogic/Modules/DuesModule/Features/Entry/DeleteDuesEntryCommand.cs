using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Entry;

/// <summary>Marks an unverified operation deleted. The row and its trail stay.</summary>
public sealed record DeleteDuesEntryCommand(Guid GroupKey, Guid EntryKey) : IRequest<ServiceResult<Unit>>;

public sealed class DeleteDuesEntryCommandHandler : IRequestHandler<DeleteDuesEntryCommand, ServiceResult<Unit>>
{
    private readonly DuesAccess _access;
    private readonly IDuesUnitOfWork _dues;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public DeleteDuesEntryCommandHandler(DuesAccess access, IDuesUnitOfWork dues, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _dues = dues;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<Unit>> Handle(DeleteDuesEntryCommand request, CancellationToken cancellationToken)
    {
        var (group, failure) = await _access.OpenGroupAsync<Unit>(request.GroupKey, cancellationToken);
        if (group is null)
        {
            return failure!;
        }

        var entry = await _dues.DuesEntries.GetByKeyAsync(request.EntryKey, cancellationToken);
        if (entry is null || entry.GroupKey != group.GroupKey || entry.IsDeleted)
        {
            return DuesErrors.NoEntry<Unit>();
        }

        if (DuesEntryLockRules.Check(entry) is { } locked)
        {
            return locked;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        entry.DeletedAtUtc = now;
        entry.DeletedByUserKey = _currentUser.UserId;
        DuesEntryTrail.Record(entry, DuesEntryTrail.Deleted, _currentUser.UserId, now);
        await _dues.SaveChangesAsync(cancellationToken);

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
