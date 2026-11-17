using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Entry;

/// <summary>
/// The впорядник's mark: "I looked, it is right". Setting it locks the operation; taking it off
/// opens it again. Both leave a trail.
/// </summary>
public sealed record SetDuesEntryVerifiedCommand(Guid GroupKey, Guid EntryKey, bool IsVerified) : IRequest<ServiceResult<Unit>>;

public sealed class SetDuesEntryVerifiedCommandHandler : IRequestHandler<SetDuesEntryVerifiedCommand, ServiceResult<Unit>>
{
    private readonly DuesAccess _access;
    private readonly IDuesUnitOfWork _dues;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public SetDuesEntryVerifiedCommandHandler(DuesAccess access, IDuesUnitOfWork dues, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _dues = dues;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<Unit>> Handle(SetDuesEntryVerifiedCommand request, CancellationToken cancellationToken)
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

        if (entry.IsVerified == request.IsVerified)
        {
            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
        }

        var now = _time.GetUtcNow().UtcDateTime;
        entry.VerifiedAtUtc = request.IsVerified ? now : null;
        entry.VerifiedByUserKey = request.IsVerified ? _currentUser.UserId : null;
        DuesEntryTrail.Record(entry, request.IsVerified ? DuesEntryTrail.Verified : DuesEntryTrail.Unverified, _currentUser.UserId, now);
        await _dues.SaveChangesAsync(cancellationToken);

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
