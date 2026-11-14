using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Kurin;

public sealed record DeleteKurinDuesEntryCommand(Guid KurinKey, Guid EntryKey) : IRequest<ServiceResult<Unit>>;

public sealed class DeleteKurinDuesEntryCommandHandler : IRequestHandler<DeleteKurinDuesEntryCommand, ServiceResult<Unit>>
{
    private readonly DuesAccess _access;
    private readonly IDuesUnitOfWork _dues;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public DeleteKurinDuesEntryCommandHandler(DuesAccess access, IDuesUnitOfWork dues, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _dues = dues;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<Unit>> Handle(DeleteKurinDuesEntryCommand request, CancellationToken cancellationToken)
    {
        if (_access.Refuse<Unit>(request.KurinKey) is { } refused)
        {
            return refused;
        }

        var entry = await _access.OwnEntryAsync(request.KurinKey, request.EntryKey, cancellationToken);
        if (entry is null)
        {
            return DuesErrors.NoEntry<Unit>();
        }

        if (entry.IsVerified)
        {
            return DuesErrors.Locked<Unit>("Звʼязковий");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        entry.DeletedAtUtc = now;
        entry.DeletedByUserKey = _currentUser.UserId;
        DuesEntryTrail.Record(entry, DuesEntryTrail.Deleted, _currentUser.UserId, now);
        await _dues.SaveChangesAsync(cancellationToken);

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
