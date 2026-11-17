using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Probe;

public sealed class UpdateProbeProgressStatusCommand : IRequest<ServiceResult<ProbeProgressDto>>
{
    public UpdateProbeProgressStatusCommand(Guid memberKey, string probeId, ProbeProgressStatus status, string? note)
    {
        MemberKey = memberKey;
        ProbeId = probeId;
        Status = status;
        Note = note;
    }

    public Guid MemberKey { get; }
    public string ProbeId { get; }
    public ProbeProgressStatus Status { get; }
    public string? Note { get; }
}

public sealed class UpdateProbeProgressStatusCommandHandler : IRequestHandler<UpdateProbeProgressStatusCommand, ServiceResult<ProbeProgressDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly ICurrentMember _actors;
    private readonly IKurinAudit _audit;
    private readonly IKurinJournalRepository _journal;
    private readonly IProbesCatalogReader _probesCatalogReader;
    private readonly IResourceAccessService _access;
    private readonly IMemberProbeLine _line;

    public UpdateProbeProgressStatusCommandHandler(
        IUnitOfWork unitOfWork,
        IMemberDirectory members,
        ICurrentMember actors,
        IKurinAudit audit,
        IAuditUnitOfWork journal,
        IProbesCatalogReader probesCatalogReader,
        IResourceAccessService access,
        IMemberProbeLine line)
    {
        _line = line;
        _unitOfWork = unitOfWork;
        _members = members;
        _actors = actors;
        _audit = audit;
        _journal = journal.KurinJournal;
        _probesCatalogReader = probesCatalogReader;
        _access = access;
    }

    public async Task<ServiceResult<ProbeProgressDto>> Handle(UpdateProbeProgressStatusCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProbeId) || request.Status == ProbeProgressStatus.NotStarted)
        {
            return ServiceResult<ProbeProgressDto>.Failure(ResultType.BadRequest, ProbesAndBadgesErrors.InvalidProbeStatus, "A probe id and a status other than NotStarted are required.");
        }

        var memberKurinKey = await _members.FindKurinKeyAsync(request.MemberKey, cancellationToken);
        if (memberKurinKey is null)
        {
            return ServiceResult<ProbeProgressDto>.Failure(ResultType.NotFound, ProbesAndBadgesErrors.MemberNotFound, "Member not found.");
        }

        var progress = await _unitOfWork.ProbeProgresses
            .GetByMemberAndProbeIdAsync(request.MemberKey, request.ProbeId, cancellationToken);

        if (progress is null)
        {
            progress = new ProbeProgress
            {
                MemberKey = request.MemberKey,
                KurinKey = memberKurinKey.Value,
                ProbeId = request.ProbeId.Trim(),
                Status = ProbeProgressStatus.NotStarted
            };

            _unitOfWork.ProbeProgresses.Create(progress, cancellationToken);
        }

        var currentStatus = progress.Status;
        if (currentStatus == request.Status)
        {
            return await RespondAsync(ResultType.Success, progress, request, cancellationToken);
        }

        if (!IsTransitionAllowed(currentStatus, request.Status))
        {
            return await RespondAsync(ResultType.Conflict, progress, request, cancellationToken);
        }

        if (currentStatus == ProbeProgressStatus.NotStarted
            && ProbeStartRules.Of<ProbeProgressDto>(
                await _line.CanStartAsync(request.MemberKey, progress.ProbeId, cancellationToken)) is { } refusal)
        {
            return refusal;
        }

        // Taking a проба back from verified undoes a sign-off, which the kurin may keep from people
        // it lets sign.
        if (currentStatus == ProbeProgressStatus.Verified
            && !(await _access.CheckCapabilityAsync(KurinCapability.UnsignProbe, ResourceType.Member, request.MemberKey, cancellationToken)).IsAllowed)
        {
            return ServiceResult<ProbeProgressDto>.Failure(
                ResultType.Forbidden,
                ProbesAndBadgesErrors.ProbeUnsignNotAllowed,
                "The kurin's policy does not let you take a probe back from verified.");
        }

        if (request.Status == ProbeProgressStatus.Completed
            && !await HasAllPointsSignedForCompletionAsync(request.MemberKey, request.ProbeId, cancellationToken))
        {
            return await RespondAsync(ResultType.Conflict, progress, request, cancellationToken);
        }

        var now = DateTime.UtcNow;
        var actor = await _actors.ActorAsync(cancellationToken);

        progress.Status = request.Status;

        ApplyStatusSideEffects(progress, currentStatus, request.Status, now, actor.UserKey, actor.Name, actor.Role);

        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Probe,
                JournalAction(currentStatus, request.Status),
                progress.ProbeProgressKey,
                ProgressAuditNames.Probe(_probesCatalogReader, progress.ProbeId),
                request.MemberKey,
                AuditChangeRules.Of(("status", currentStatus, request.Status)),
                request.Note),
            cancellationToken);

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (changes <= 0)
        {
            return ServiceResult<ProbeProgressDto>.Failure(ResultType.InternalServerError, ProbesAndBadgesErrors.NotSaved, "The probe status was not saved.");
        }

        return await RespondAsync(ResultType.Success, progress, request, cancellationToken);
    }

    private async Task<ServiceResult<ProbeProgressDto>> RespondAsync(
        ResultType resultType,
        ProbeProgress progress,
        UpdateProbeProgressStatusCommand request,
        CancellationToken cancellationToken)
    {
        var pointSignatures = await GetPointSignatureResponsesAsync(request.MemberKey, request.ProbeId, cancellationToken);
        var journal = await _journal.GetForSubjectsAsync([progress.ProbeProgressKey], cancellationToken);
        return new ServiceResult<ProbeProgressDto>(
            resultType,
            ProbeMapping.ToDto(progress, journal, pointSignatures));
    }

    private async Task<IReadOnlyCollection<ProbePointProgressDto>> GetPointSignatureResponsesAsync(
        Guid memberKey,
        string probeId,
        CancellationToken cancellationToken)
    {
        var pointSignatures = await _unitOfWork.ProbePointProgresses
            .GetByMemberAndProbeAsync(memberKey, probeId.Trim(), cancellationToken);

        return pointSignatures
            .OrderBy(x => x.PointId)
            .Select(x => new ProbePointProgressDto(
                x.ProbePointProgressKey,
                x.PointId,
                x.IsSigned,
                x.SignedAtUtc,
                x.SignedByUserKey,
                x.SignedByName,
                x.SignedByRole))
            .ToList();
    }

    private async Task<bool> HasAllPointsSignedForCompletionAsync(
        Guid memberKey,
        string probeId,
        CancellationToken cancellationToken)
    {
        var groupedProbe = _probesCatalogReader.GetGroupedProbeById(probeId.Trim());
        if (groupedProbe is null || groupedProbe.PointsCount <= 0)
        {
            return true;
        }

        var pointSignatures = await _unitOfWork.ProbePointProgresses
            .GetByMemberAndProbeAsync(memberKey, probeId.Trim(), cancellationToken);

        var signedPointCount = pointSignatures
            .Where(x => x.IsSigned)
            .Select(x => x.PointId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return signedPointCount >= groupedProbe.PointsCount;
    }

    private static bool IsTransitionAllowed(ProbeProgressStatus current, ProbeProgressStatus target)
    {
        return current switch
        {
            ProbeProgressStatus.NotStarted => target is ProbeProgressStatus.InProgress or ProbeProgressStatus.Completed,
            ProbeProgressStatus.InProgress => target == ProbeProgressStatus.Completed,
            ProbeProgressStatus.Completed => target == ProbeProgressStatus.Verified,
            ProbeProgressStatus.Verified => target == ProbeProgressStatus.Completed,
            _ => false
        };
    }

    private static string JournalAction(ProbeProgressStatus currentStatus, ProbeProgressStatus targetStatus) => targetStatus switch
    {
        ProbeProgressStatus.Completed when currentStatus == ProbeProgressStatus.Verified => AuditActionNames.Probe.Unverified,
        ProbeProgressStatus.Completed => AuditActionNames.Probe.Completed,
        ProbeProgressStatus.Verified => AuditActionNames.Probe.Verified,
        _ => AuditActionNames.Probe.Started
    };

    private static void ApplyStatusSideEffects(
        ProbeProgress progress,
        ProbeProgressStatus currentStatus,
        ProbeProgressStatus targetStatus,
        DateTime now,
        Guid? actorUserKey,
        string? actorName,
        string actorRole)
    {
        if (targetStatus == ProbeProgressStatus.Completed)
        {
            if (currentStatus == ProbeProgressStatus.Verified)
            {
                progress.VerifiedAtUtc = null;
                progress.VerifiedByUserKey = null;
                progress.VerifiedByName = null;
                progress.VerifiedByRole = null;
                return;
            }

            progress.CompletedAtUtc = now;
            progress.CompletedByUserKey = actorUserKey;
            progress.CompletedByName = actorName;
            progress.CompletedByRole = actorRole;
            return;
        }

        if (targetStatus == ProbeProgressStatus.Verified)
        {
            progress.VerifiedAtUtc = now;
            progress.VerifiedByUserKey = actorUserKey;
            progress.VerifiedByName = actorName;
            progress.VerifiedByRole = actorRole;
            return;
        }

        if (targetStatus == ProbeProgressStatus.InProgress)
        {
            progress.CompletedAtUtc = null;
            progress.CompletedByUserKey = null;
            progress.CompletedByName = null;
            progress.CompletedByRole = null;
            progress.VerifiedAtUtc = null;
            progress.VerifiedByUserKey = null;
            progress.VerifiedByName = null;
            progress.VerifiedByRole = null;
        }
    }
}
