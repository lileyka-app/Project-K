using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Archive.DangerZone;

/// <summary>Why a danger zone ended, as the journal tells it.</summary>
public enum ArchiveDangerZoneEnd
{
    /// <summary>The «Вимкнути» button.</summary>
    Manual,

    /// <summary>The person left the archive page or closed the tab.</summary>
    Left
}

/// <summary>
/// Ends the caller's danger zone in the kurin. Ending one that is not there is fine: the page asks
/// on every way out, and a zone that ran out of time is journalled as such whatever the page says.
/// </summary>
public sealed record EndArchiveDangerZoneCommand(Guid KurinKey, ArchiveDangerZoneEnd Reason) : IRequest<ServiceResult<object>>;

public sealed class EndArchiveDangerZoneCommandHandler : IRequestHandler<EndArchiveDangerZoneCommand, ServiceResult<object>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ArchiveDangerZone _zone;
    private readonly ICurrentUserContext _currentUser;
    private readonly IKurinAudit _audit;

    public EndArchiveDangerZoneCommandHandler(IUnitOfWork unitOfWork, ArchiveDangerZone zone, ICurrentUserContext currentUser, IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _zone = zone;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<ServiceResult<object>> Handle(EndArchiveDangerZoneCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userKey || _zone.Close(userKey, request.KurinKey) is not { } timedOut)
        {
            return new ServiceResult<object>(ResultType.Success);
        }

        var reason = timedOut ? "time" : request.Reason == ArchiveDangerZoneEnd.Left ? "left" : "manual";
        await _audit.RecordAsync(
            new AuditRecord(AuditArea.Archive, AuditActions.Archive.DangerZoneEnded, userKey, "Небезпечний режим", Changes: AuditChanges.Added(("reason", reason)), KurinKey: request.KurinKey),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceResult<object>(ResultType.Success);
    }
}
