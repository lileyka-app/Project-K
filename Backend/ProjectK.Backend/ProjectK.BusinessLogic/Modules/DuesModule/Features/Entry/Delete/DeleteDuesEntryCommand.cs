using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Entry.Delete;

/// <summary>Marks an unverified operation deleted. The row and its trail stay.</summary>
public sealed record DeleteDuesEntryCommand(Guid GroupKey, Guid EntryKey) : IRequest<ServiceResult<object>>;

public sealed class DeleteDuesEntryCommandHandler : IRequestHandler<DeleteDuesEntryCommand, ServiceResult<object>>
{
    private readonly GroupDuesAccess _access;
    private readonly IDuesUnitOfWork _dues;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public DeleteDuesEntryCommandHandler(GroupDuesAccess access, IDuesUnitOfWork dues, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _dues = dues;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<object>> Handle(DeleteDuesEntryCommand request, CancellationToken cancellationToken)
    {
        var (group, failure) = await _access.OpenAsync<object>(request.GroupKey, cancellationToken);
        if (group is null)
        {
            return failure!;
        }

        var entry = await _dues.DuesEntries.GetByKeyAsync(request.EntryKey, cancellationToken);
        if (entry is null || entry.GroupKey != group.GroupKey || entry.IsDeleted)
        {
            return ServiceResult<object>.Failure(ResultType.NotFound, "EntryNotFound", "There is no such operation here.");
        }

        if (DuesEntryLock.Check(entry) is { } locked)
        {
            return locked;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        entry.DeletedAtUtc = now;
        entry.DeletedByUserKey = _currentUser.UserId;
        DuesEntryTrail.Record(entry, DuesEntryTrail.Deleted, _currentUser.UserId, now);
        await _dues.SaveChangesAsync(cancellationToken);

        return new ServiceResult<object>(ResultType.Success);
    }
}
