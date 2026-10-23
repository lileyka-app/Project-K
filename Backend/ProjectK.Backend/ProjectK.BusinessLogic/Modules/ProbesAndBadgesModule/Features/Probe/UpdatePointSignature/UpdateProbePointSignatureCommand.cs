using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.BusinessLogic.Services.Audit;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuditModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Probe.UpdatePointSignature;

public sealed class UpdateProbePointSignatureCommand : IRequest<ServiceResult<ProbeProgressResponse>>
{
    public UpdateProbePointSignatureCommand(Guid memberKey, string probeId, string pointId, bool isSigned, string? note)
    {
        MemberKey = memberKey;
        ProbeId = probeId;
        PointId = pointId;
        IsSigned = isSigned;
        Note = note;
    }

    public Guid MemberKey { get; }
    public string ProbeId { get; }
    public string PointId { get; }
    public bool IsSigned { get; }
    public string? Note { get; }
}

public sealed class UpdateProbePointSignatureCommandHandler : IRequestHandler<UpdateProbePointSignatureCommand, ServiceResult<ProbeProgressResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly AuditActorResolver _actors;
    private readonly IKurinAudit _audit;
    private readonly IKurinJournalReader _journal;
    private readonly IProbesCatalogService _probes;

    public UpdateProbePointSignatureCommandHandler(
        IUnitOfWork unitOfWork,
        IMemberDirectory members,
        AuditActorResolver actors,
        IKurinAudit audit,
        IKurinJournalReader journal,
        IProbesCatalogService probes)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _actors = actors;
        _audit = audit;
        _journal = journal;
        _probes = probes;
    }

    public async Task<ServiceResult<ProbeProgressResponse>> Handle(UpdateProbePointSignatureCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProbeId) || string.IsNullOrWhiteSpace(request.PointId))
        {
            return new ServiceResult<ProbeProgressResponse>(ResultType.BadRequest);
        }

        var normalizedProbeId = request.ProbeId.Trim();
        var normalizedPointId = request.PointId.Trim();

        var memberKurinKey = await _members.FindKurinKeyAsync(request.MemberKey, cancellationToken);
        if (memberKurinKey is null)
        {
            return new ServiceResult<ProbeProgressResponse>(ResultType.NotFound);
        }

        var pointProgress = await _unitOfWork.ProbePointProgresses
            .GetByMemberProbePointAsync(request.MemberKey, normalizedProbeId, normalizedPointId, cancellationToken);

        var probeProgress = await _unitOfWork.ProbeProgresses
            .GetByMemberAndProbeIdAsync(request.MemberKey, normalizedProbeId, cancellationToken);

        var actor = await _actors.ResolveAsync(cancellationToken);
        var actorName = actor.Name;
        var now = DateTime.UtcNow;
        var pointUpdateContext = new PointSignatureUpdateContext(
            IsSigned: request.IsSigned,
            MemberKey: request.MemberKey,
            KurinKey: memberKurinKey.Value,
            ProbeId: normalizedProbeId,
            PointId: normalizedPointId,
            TimestampUtc: now,
            ActorUserKey: actor.UserKey,
            ActorRole: actor.Role,
            ActorName: actorName,
            CancellationToken: cancellationToken);

        var pointUpdate = ApplyPointSignatureUpdate(
            pointProgress,
            pointUpdateContext);

        var statusBefore = probeProgress?.Status;
        var probeUpdate = ApplyProbeProgressUpdate(
            probeProgress,
            request.IsSigned,
            pointUpdate.PointWasActuallyUnsigned,
            request.MemberKey,
            memberKurinKey.Value,
            normalizedProbeId,
            cancellationToken);
        probeProgress = probeUpdate.ProbeProgress;

        var hasChanges = pointUpdate.HasChanges || probeUpdate.HasChanges;

        if (hasChanges)
        {
            await RecordAsync(
                request,
                pointUpdate.PointProgress,
                probeProgress,
                statusBefore,
                pointUpdate.HasChanges,
                pointUpdate.PointWasActuallyUnsigned,
                cancellationToken);

            var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (changes <= 0)
            {
                return new ServiceResult<ProbeProgressResponse>(ResultType.InternalServerError);
            }
        }

        var latestPointSignatures = await BuildPointSignatureResponsesAsync(request.MemberKey, normalizedProbeId, cancellationToken);

        if (probeProgress is null)
        {
            var notStarted = ProbeProgressResponse.CreateNotStarted(
                request.MemberKey,
                memberKurinKey.Value,
                normalizedProbeId,
                latestPointSignatures);
            return new ServiceResult<ProbeProgressResponse>(ResultType.Success, notStarted);
        }

        var journal = await _journal.GetForSubjectsAsync([probeProgress.ProbeProgressKey], cancellationToken);
        return new ServiceResult<ProbeProgressResponse>(
            ResultType.Success,
            ProbeProgressResponse.FromEntity(probeProgress, journal, latestPointSignatures));
    }

    /// <summary>
    /// The journal entries of one signature change: the point signed or unsigned, and — when the
    /// unsigned point belonged to a проба already signed off — the проба going back to work, which
    /// the unsign does on its own and nothing else records.
    /// </summary>
    private async Task RecordAsync(
        UpdateProbePointSignatureCommand request,
        ProbePointProgress? pointProgress,
        ProbeProgress? probeProgress,
        ProbeProgressStatus? statusBefore,
        bool pointChanged,
        bool pointWasUnsigned,
        CancellationToken cancellationToken)
    {
        var probeId = request.ProbeId.Trim();
        var pointId = request.PointId.Trim();
        var subjectKey = probeProgress?.ProbeProgressKey ?? pointProgress?.ProbePointProgressKey ?? Guid.Empty;
        var probeLabel = ProgressAuditLabels.Probe(_probes, probeId);

        if (pointChanged)
        {
            var pointLabel = ProgressAuditLabels.ProbePoint(_probes, probeId, pointId);
            await _audit.RecordAsync(
                new AuditRecord(
                    AuditArea.Probe,
                    request.IsSigned ? AuditActions.Probe.PointSigned : AuditActions.Probe.PointUnsigned,
                    subjectKey,
                    probeLabel,
                    request.MemberKey,
                    request.IsSigned
                        ? AuditChanges.Added(("point", pointLabel))
                        : AuditChanges.Removed(("point", pointLabel)),
                    request.Note),
                cancellationToken);
        }

        if (pointWasUnsigned && statusBefore is { } before && IsCompletedOrVerified(before) && probeProgress is not null)
        {
            await _audit.RecordAsync(
                new AuditRecord(
                    AuditArea.Probe,
                    AuditActions.Probe.Reopened,
                    subjectKey,
                    probeLabel,
                    request.MemberKey,
                    AuditChanges.Of(("status", before, probeProgress.Status))),
                cancellationToken);
        }
    }

    private (ProbePointProgress? PointProgress, bool HasChanges, bool PointWasActuallyUnsigned) ApplyPointSignatureUpdate(
        ProbePointProgress? pointProgress,
        PointSignatureUpdateContext context)
    {
        if (context.IsSigned)
        {
            if (pointProgress is null)
            {
                pointProgress = new ProbePointProgress
                {
                    MemberKey = context.MemberKey,
                    KurinKey = context.KurinKey,
                    ProbeId = context.ProbeId,
                    PointId = context.PointId,
                    IsSigned = true,
                    SignedAtUtc = context.TimestampUtc,
                    SignedByUserKey = context.ActorUserKey,
                    SignedByName = context.ActorName,
                    SignedByRole = context.ActorRole
                };
                _unitOfWork.ProbePointProgresses.Create(pointProgress, context.CancellationToken);
                return (pointProgress, true, false);
            }

            if (!pointProgress.IsSigned)
            {
                pointProgress.IsSigned = true;
                pointProgress.SignedAtUtc = context.TimestampUtc;
                pointProgress.SignedByUserKey = context.ActorUserKey;
                pointProgress.SignedByName = context.ActorName;
                pointProgress.SignedByRole = context.ActorRole;
                _unitOfWork.ProbePointProgresses.Update(pointProgress, context.CancellationToken);
                return (pointProgress, true, false);
            }

            return (pointProgress, false, false);
        }

        if (pointProgress is null || !pointProgress.IsSigned)
        {
            return (pointProgress, false, false);
        }

        pointProgress.IsSigned = false;
        pointProgress.SignedAtUtc = null;
        pointProgress.SignedByUserKey = null;
        pointProgress.SignedByName = null;
        pointProgress.SignedByRole = null;
        _unitOfWork.ProbePointProgresses.Update(pointProgress, context.CancellationToken);

        return (pointProgress, true, true);
    }

    private sealed record PointSignatureUpdateContext(
        bool IsSigned,
        Guid MemberKey,
        Guid KurinKey,
        string ProbeId,
        string PointId,
        DateTime TimestampUtc,
        Guid? ActorUserKey,
        string? ActorRole,
        string? ActorName,
        CancellationToken CancellationToken);

    private (ProbeProgress? ProbeProgress, bool HasChanges) ApplyProbeProgressUpdate(
        ProbeProgress? probeProgress,
        bool isSigned,
        bool pointWasActuallyUnsigned,
        Guid memberKey,
        Guid kurinKey,
        string probeId,
        CancellationToken cancellationToken)
    {
        if (probeProgress is null)
        {
            if (!isSigned)
            {
                return (null, false);
            }

            var createdProbeProgress = new ProbeProgress
            {
                MemberKey = memberKey,
                KurinKey = kurinKey,
                ProbeId = probeId,
                Status = ProbeProgressStatus.InProgress
            };
            _unitOfWork.ProbeProgresses.Create(createdProbeProgress, cancellationToken);
            return (createdProbeProgress, true);
        }

        if (!isSigned && pointWasActuallyUnsigned && IsCompletedOrVerified(probeProgress.Status))
        {
            probeProgress.Status = ProbeProgressStatus.InProgress;
            probeProgress.CompletedAtUtc = null;
            probeProgress.CompletedByUserKey = null;
            probeProgress.CompletedByName = null;
            probeProgress.CompletedByRole = null;
            probeProgress.VerifiedAtUtc = null;
            probeProgress.VerifiedByUserKey = null;
            probeProgress.VerifiedByName = null;
            probeProgress.VerifiedByRole = null;
            return (probeProgress, true);
        }

        if (isSigned && probeProgress.Status == ProbeProgressStatus.NotStarted)
        {
            probeProgress.Status = ProbeProgressStatus.InProgress;
            return (probeProgress, true);
        }

        return (probeProgress, false);
    }

    private static bool IsCompletedOrVerified(ProbeProgressStatus status)
    {
        return status == ProbeProgressStatus.Completed || status == ProbeProgressStatus.Verified;
    }

    private async Task<IReadOnlyCollection<ProbePointProgressResponse>> BuildPointSignatureResponsesAsync(
        Guid memberKey,
        string probeId,
        CancellationToken cancellationToken)
    {
        var pointSignatures = await _unitOfWork.ProbePointProgresses
            .GetByMemberAndProbeAsync(memberKey, probeId, cancellationToken);

        return pointSignatures
            .OrderBy(x => x.PointId)
            .Select(x => new ProbePointProgressResponse(
                x.ProbePointProgressKey,
                x.PointId,
                x.IsSigned,
                x.SignedAtUtc,
                x.SignedByUserKey,
                x.SignedByName,
                x.SignedByRole))
            .ToList();
    }

}
