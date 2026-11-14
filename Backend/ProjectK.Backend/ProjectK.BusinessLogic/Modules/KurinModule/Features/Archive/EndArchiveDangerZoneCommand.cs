using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Archive;

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
public sealed record EndArchiveDangerZoneCommand(Guid KurinKey, ArchiveDangerZoneEnd Reason) : IRequest<ServiceResult<Unit>>;

public sealed class EndArchiveDangerZoneCommandHandler : IRequestHandler<EndArchiveDangerZoneCommand, ServiceResult<Unit>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ArchiveDangerZoneService _zone;
    private readonly ICurrentUserContext _currentUser;
    private readonly IKurinAudit _audit;

    public EndArchiveDangerZoneCommandHandler(IUnitOfWork unitOfWork, ArchiveDangerZoneService zone, ICurrentUserContext currentUser, IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _zone = zone;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<ServiceResult<Unit>> Handle(EndArchiveDangerZoneCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userKey || _zone.Close(userKey, request.KurinKey) is not { } timedOut)
        {
            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
        }

        var reason = timedOut ? "time" : request.Reason == ArchiveDangerZoneEnd.Left ? "left" : "manual";
        await _audit.RecordAsync(
            ArchiveDangerZoneService.JournalEntry(AuditActions.Archive.DangerZoneEnded, userKey, request.KurinKey, "reason", reason),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
