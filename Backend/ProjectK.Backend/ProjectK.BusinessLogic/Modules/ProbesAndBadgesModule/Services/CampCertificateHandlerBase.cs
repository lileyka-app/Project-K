using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>What both посвідка handlers share: their dependencies and the journal line.</summary>
public abstract class CampCertificateHandlerBase
{
    protected static ServiceResult<ThirdProbeDto> StorageFull() =>
        ProbeProjectGuard.Refuse<ThirdProbeDto>(
            ProbesAndBadgesErrors.StorageFull, $"The third probe's files would take more than {ProbeProjectRules.MaxStoredBytes / (1024 * 1024)} MB.");

    protected CampCertificateHandlerBase(
        IUnitOfWork unitOfWork,
        IProbeProjectAccess access,
        IPhotoService photos,
        IKurinAudit audit,
        IProbesCatalogService probes,
        IThirdProbeReader reader)
    {
        UnitOfWork = unitOfWork;
        Access = access;
        Photos = photos;
        Audit = audit;
        Probes = probes;
        Reader = reader;
    }

    protected IUnitOfWork UnitOfWork { get; }
    protected IProbeProjectAccess Access { get; }
    protected IPhotoService Photos { get; }
    protected IKurinAudit Audit { get; }
    protected IProbesCatalogService Probes { get; }
    protected IThirdProbeReader Reader { get; }

    protected Task RecordAsync(string action, ThirdProbeFile file, Guid memberKey, string probeId, CancellationToken cancellationToken) =>
        Audit.RecordAsync(
            new AuditRecord(AuditArea.Probe, action, file.ThirdProbeFileKey, ProgressAuditLabels.Probe(Probes, probeId), memberKey),
            cancellationToken);
}
