using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// What every deletion from the archive passes first and last: the right to delete across the
/// kurin and a live danger zone on the way in, one deletion counted off on the way out. The zone
/// that has none left ends there and then, and the journal says why.
/// </summary>
public sealed class ArchiveDeletionGate
{
    private readonly ArchiveReach _reach;
    private readonly ArchiveDangerZone _zone;
    private readonly ICurrentUserContext _currentUser;
    private readonly IKurinAudit _audit;

    public ArchiveDeletionGate(ArchiveReach reach, ArchiveDangerZone zone, ICurrentUserContext currentUser, IKurinAudit audit)
    {
        _reach = reach;
        _zone = zone;
        _currentUser = currentUser;
        _audit = audit;
    }

    public sealed record Pass(ArchiveReachResult Reach, ArchiveDangerZoneEntry Zone);

    /// <summary>The pass, or why there is none.</summary>
    public async Task<(Pass? Pass, ServiceResult<ArchiveDeletionDto>? Refusal)> OpenAsync(Guid kurinKey, string? ticket, CancellationToken cancellationToken)
    {
        if (await _reach.ResolveAsync(kurinKey, ResourceAction.Delete, cancellationToken) is not { KurinWide: true } reach)
        {
            return (null, ServiceResult<ArchiveDeletionDto>.Failure(ResultType.Forbidden, ArchiveErrors.OutOfReach, "Only the Звʼязковий deletes from the archive."));
        }

        return await _zone.CheckAsync(ticket, kurinKey, cancellationToken) is { } zone
            ? (new Pass(reach, zone), null)
            : (null, ServiceResult<ArchiveDeletionDto>.Failure(ResultType.Forbidden, ArchiveErrors.DangerZoneClosed, "The danger zone is off."));
    }

    /// <summary>Counts the deletion off; null when the zone closed under the request.</summary>
    public async Task<int?> SpendAsync(Pass pass, Guid kurinKey, CancellationToken cancellationToken)
    {
        if (_zone.Spend(pass.Zone) is not { } left)
        {
            return null;
        }

        if (left == 0 && _currentUser.UserId is { } userKey && _zone.Close(userKey, kurinKey) is not null)
        {
            await _audit.RecordAsync(
                new AuditRecord(AuditArea.Archive, AuditActions.Archive.DangerZoneEnded, userKey, "Небезпечний режим", Changes: AuditChanges.Added(("reason", "spent")), KurinKey: kurinKey),
                cancellationToken);
        }

        return left;
    }

    public static ServiceResult<ArchiveDeletionDto> Closed() =>
        ServiceResult<ArchiveDeletionDto>.Failure(ResultType.Forbidden, ArchiveErrors.DangerZoneClosed, "The danger zone is off.");
}
