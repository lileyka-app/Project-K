using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Kurin;

/// <summary>
/// The курінний скарбник confirms that a гурток's transfer arrived — or takes the confirmation back.
/// Until it is confirmed the money is in transit: out of the гурток's box, not yet in the kurin's.
/// </summary>
public sealed record SetDuesTransferReceivedCommand(Guid KurinKey, Guid EntryKey, bool IsReceived) : IRequest<ServiceResult<Unit>>;

public sealed class SetDuesTransferReceivedCommandHandler : IRequestHandler<SetDuesTransferReceivedCommand, ServiceResult<Unit>>
{
    private readonly KurinDuesAccess _access;
    private readonly IDuesUnitOfWork _dues;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public SetDuesTransferReceivedCommandHandler(KurinDuesAccess access, IDuesUnitOfWork dues, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _dues = dues;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<Unit>> Handle(SetDuesTransferReceivedCommand request, CancellationToken cancellationToken)
    {
        if (_access.Refuse<Unit>(request.KurinKey) is { } refused)
        {
            return refused;
        }

        var transfer = await _access.TransferAsync(request.KurinKey, request.EntryKey, cancellationToken);
        if (transfer is null)
        {
            return ServiceResult<Unit>.Failure(ResultType.NotFound, DuesErrors.TransferNotFound, "There is no such transfer to this kurin.");
        }

        if (transfer.ReceivedAtUtc.HasValue == request.IsReceived)
        {
            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
        }

        var now = _time.GetUtcNow().UtcDateTime;
        transfer.ReceivedAtUtc = request.IsReceived ? now : null;
        transfer.ReceivedByUserKey = request.IsReceived ? _currentUser.UserId : null;
        DuesEntryTrail.Record(transfer, request.IsReceived ? DuesEntryTrail.Received : DuesEntryTrail.Unreceived, _currentUser.UserId, now);
        await _dues.SaveChangesAsync(cancellationToken);

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
