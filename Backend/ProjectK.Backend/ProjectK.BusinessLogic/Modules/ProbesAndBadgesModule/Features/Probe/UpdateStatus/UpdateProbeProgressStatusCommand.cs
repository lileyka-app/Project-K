using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Probe.UpdateStatus;

public sealed class UpdateProbeProgressStatusCommand : IRequest<ServiceResult<ProbeProgressResponse>>
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

public sealed class UpdateProbeProgressStatusCommandHandler : IRequestHandler<UpdateProbeProgressStatusCommand, ServiceResult<ProbeProgressResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IProbesCatalogService _probesCatalogService;
    private readonly IResourceAccessService _access;

    public UpdateProbeProgressStatusCommandHandler(
        IUnitOfWork unitOfWork,
        IMemberDirectory members,
        ICurrentUserContext currentUserContext,
        IProbesCatalogService probesCatalogService,
        IResourceAccessService access)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _currentUserContext = currentUserContext;
        _probesCatalogService = probesCatalogService;
        _access = access;
    }

    public async Task<ServiceResult<ProbeProgressResponse>> Handle(UpdateProbeProgressStatusCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProbeId) || request.Status == ProbeProgressStatus.NotStarted)
        {
            return new ServiceResult<ProbeProgressResponse>(ResultType.BadRequest);
        }

        var memberKurinKey = await _members.FindKurinKeyAsync(request.MemberKey, cancellationToken);
        if (memberKurinKey is null)
        {
            return new ServiceResult<ProbeProgressResponse>(ResultType.NotFound);
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
            var pointSignatures = await GetPointSignatureResponsesAsync(request.MemberKey, request.ProbeId, cancellationToken);
            return new ServiceResult<ProbeProgressResponse>(
                ResultType.Success,
                ProbeProgressResponse.FromEntity(progress, pointSignatures));
        }

        if (!IsTransitionAllowed(currentStatus, request.Status))
        {
            var pointSignatures = await GetPointSignatureResponsesAsync(request.MemberKey, request.ProbeId, cancellationToken);
            return new ServiceResult<ProbeProgressResponse>(
                ResultType.Conflict,
                ProbeProgressResponse.FromEntity(progress, pointSignatures));
        }

        // Taking a проба back from verified undoes a sign-off, which the kurin may keep from people
        // it lets sign.
        if (currentStatus == ProbeProgressStatus.Verified
            && !(await _access.CheckCapabilityAsync(KurinCapability.UnsignProbe, ResourceType.Member, request.MemberKey, cancellationToken)).IsAllowed)
        {
            return ServiceResult<ProbeProgressResponse>.Failure(
                ResultType.Forbidden,
                "ProbeUnsignNotAllowed",
                "The kurin's policy does not let you take a probe back from verified.");
        }

        if (request.Status == ProbeProgressStatus.Completed
            && !await HasAllPointsSignedForCompletionAsync(request.MemberKey, request.ProbeId, cancellationToken))
        {
            var pointSignatures = await GetPointSignatureResponsesAsync(request.MemberKey, request.ProbeId, cancellationToken);
            return new ServiceResult<ProbeProgressResponse>(
                ResultType.Conflict,
                ProbeProgressResponse.FromEntity(progress, pointSignatures));
        }

        var now = DateTime.UtcNow;
        var actor = await ProgressActorResolver.ResolveAsync(_currentUserContext, _members, cancellationToken);
        var actorName = actor.Name;
        var auditAction = ResolveAuditAction(currentStatus, request.Status);

        progress.Status = request.Status;

        ApplyStatusSideEffects(progress, currentStatus, request.Status, now, actor.UserKey, actorName, actor.Role);

        progress.AuditEvents.Add(new ProbeProgressAuditEvent
        {
            FromStatus = currentStatus,
            ToStatus = request.Status,
            Action = auditAction,
            ActorUserKey = actor.UserKey,
            ActorName = actorName,
            ActorRole = actor.Role,
            OccurredAtUtc = now,
            Note = request.Note
        });

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (changes <= 0)
        {
            return new ServiceResult<ProbeProgressResponse>(ResultType.InternalServerError);
        }

        var updatedPointSignatures = await GetPointSignatureResponsesAsync(request.MemberKey, request.ProbeId, cancellationToken);
        return new ServiceResult<ProbeProgressResponse>(
            ResultType.Success,
            ProbeProgressResponse.FromEntity(progress, updatedPointSignatures));
    }

    private async Task<IReadOnlyCollection<ProbePointProgressResponse>> GetPointSignatureResponsesAsync(
        Guid memberKey,
        string probeId,
        CancellationToken cancellationToken)
    {
        var pointSignatures = await _unitOfWork.ProbePointProgresses
            .GetByMemberAndProbeAsync(memberKey, probeId.Trim(), cancellationToken);

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

    private async Task<bool> HasAllPointsSignedForCompletionAsync(
        Guid memberKey,
        string probeId,
        CancellationToken cancellationToken)
    {
        var groupedProbe = _probesCatalogService.GetGroupedProbeById(probeId.Trim());
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

    private static string ResolveAuditAction(ProbeProgressStatus currentStatus, ProbeProgressStatus targetStatus)
    {
        if (targetStatus == ProbeProgressStatus.InProgress
            && (currentStatus == ProbeProgressStatus.Completed || currentStatus == ProbeProgressStatus.Verified))
        {
            return "Unsign";
        }

        if (targetStatus == ProbeProgressStatus.Completed && currentStatus == ProbeProgressStatus.Verified)
        {
            return "Unverify";
        }

        return targetStatus.ToString();
    }

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
