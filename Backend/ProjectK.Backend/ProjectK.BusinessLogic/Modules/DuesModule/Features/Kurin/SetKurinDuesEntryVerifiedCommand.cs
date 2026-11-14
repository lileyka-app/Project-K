using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Kurin;

/// <summary>The Звʼязковий's mark on a kurin operation; setting it locks the operation.</summary>
public sealed record SetKurinDuesEntryVerifiedCommand(Guid KurinKey, Guid EntryKey, bool IsVerified) : IRequest<ServiceResult<Unit>>;

public sealed class SetKurinDuesEntryVerifiedCommandHandler : IRequestHandler<SetKurinDuesEntryVerifiedCommand, ServiceResult<Unit>>
{
    private readonly KurinDuesAccess _access;
    private readonly IDuesUnitOfWork _dues;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public SetKurinDuesEntryVerifiedCommandHandler(KurinDuesAccess access, IDuesUnitOfWork dues, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _dues = dues;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<Unit>> Handle(SetKurinDuesEntryVerifiedCommand request, CancellationToken cancellationToken)
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
