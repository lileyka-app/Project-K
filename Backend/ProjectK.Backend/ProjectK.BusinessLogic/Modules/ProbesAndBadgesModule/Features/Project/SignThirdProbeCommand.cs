using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.BusinessLogic.Services.Audit;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Project;

/// <summary>What a leader confirms on the third probe, besides the probe's points.</summary>
public enum ThirdProbeSignPart
{
    /// <summary>The reflection with the Звʼязковий took place. (The camp has no confirmation: the край counts it, not Lilejka.)</summary>
    Reflection,

    /// <summary>«Дозвіл»: the project may go ahead.</summary>
    ProjectApproval,

    /// <summary>«Успішно виконав»: the project is done, with the coordinator's comment.</summary>
    ProjectCompletion
}

/// <summary>
/// Confirms or takes back one part of the third probe. Confirming is the kurin's
/// <see cref="Common.Models.Authorization.KurinPolicy.KurinCapability.SignProbe"/>, taking back its
/// <see cref="Common.Models.Authorization.KurinPolicy.KurinCapability.UnsignProbe"/>, the same pair
/// that signs and unsigns probe points.
/// </summary>
public sealed record SignThirdProbeCommand(
    Guid MemberKey,
    string ProbeId,
    ThirdProbeSignPart Part,
    bool Sign,
    Guid? ProjectKey = null,
    string? Comment = null) : IRequest<ServiceResult<ThirdProbeResponse>>;

public sealed class SignThirdProbeCommandHandler : IRequestHandler<SignThirdProbeCommand, ServiceResult<ThirdProbeResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProbeProjectAccess _access;
    private readonly AuditActorResolver _actors;
    private readonly IKurinAudit _audit;
    private readonly IProbesCatalogService _probes;
    private readonly IThirdProbeReader _reader;

    public SignThirdProbeCommandHandler(
        IUnitOfWork unitOfWork,
        IProbeProjectAccess access,
        AuditActorResolver actors,
        IKurinAudit audit,
        IProbesCatalogService probes,
        IThirdProbeReader reader)
    {
        _unitOfWork = unitOfWork;
        _access = access;
        _actors = actors;
        _audit = audit;
        _probes = probes;
        _reader = reader;
    }

    public async Task<ServiceResult<ThirdProbeResponse>> Handle(SignThirdProbeCommand request, CancellationToken cancellationToken)
    {
        var probeId = request.ProbeId.Trim();
        var memberKey = request.MemberKey;
        if (await ProbeProjectGuard.RefuseAsync<ThirdProbeResponse>(
                _access, memberKey, probeId, cancellationToken,
                access => request.Sign ? access.CanSignAsync(memberKey, cancellationToken) : access.CanUnsignAsync(memberKey, cancellationToken))
            is { } refused)
        {
            return refused;
        }

        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        if (comment?.Length > ProbeProjectRules.MaxCommentLength)
        {
            return ProbeProjectGuard.Refuse<ThirdProbeResponse>("FieldTooLong", $"A comment holds at most {ProbeProjectRules.MaxCommentLength} characters.");
        }

        var actor = await _actors.ResolveAsync(cancellationToken);
        var at = request.Sign ? DateTime.UtcNow : (DateTime?)null;
        var by = request.Sign ? actor.UserKey : null;
        var name = request.Sign ? actor.Name : null;
        Guid subjectKey;
        string subject;

        if (request.Part == ThirdProbeSignPart.Reflection)
        {
            var file = await _unitOfWork.ProbeProjects.GetFileAsync(memberKey, probeId, cancellationToken);
            if (request.Sign && file?.ReflectionOn is null)
            {
                return ProbeProjectGuard.Refuse<ThirdProbeResponse>("NothingToConfirm", "Write down when the reflection took place first.");
            }

            if (file is not null)
            {
                (file.ReflectionConfirmedAtUtc, file.ReflectionConfirmedByUserKey, file.ReflectionConfirmedByName) = (at, by, name);
            }

            subjectKey = file?.ThirdProbeFileKey ?? Guid.Empty;
            subject = "reflection";
        }
        else
        {
            var project = request.ProjectKey is { } key ? await _unitOfWork.ProbeProjects.GetWithImagesAsync(key, cancellationToken) : null;
            if (project is null || project.MemberKey != memberKey || project.ProbeId != probeId)
            {
                return new ServiceResult<ThirdProbeResponse>(ResultType.NotFound);
            }

            if (request.Part == ThirdProbeSignPart.ProjectApproval)
            {
                (project.ApprovedAtUtc, project.ApprovedByUserKey, project.ApprovedByName) = (at, by, name);
            }
            else
            {
                (project.CompletedAtUtc, project.CompletedByUserKey, project.CompletedByName) = (at, by, name);
                project.CoordinatorComment = request.Sign ? comment : null;
            }

            subjectKey = project.ProbeProjectKey;
            subject = project.Title;
        }

        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Probe,
                AuditActions.Probe.ThirdProbe(request.Part.ToString(), request.Sign),
                subjectKey,
                ProgressAuditLabels.Probe(_probes, probeId),
                memberKey,
                request.Sign ? AuditChanges.Added((Field(request.Part), subject)) : AuditChanges.Removed((Field(request.Part), subject)),
                comment),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<ThirdProbeResponse>(ResultType.Success, await _reader.ReadAsync(memberKey, probeId, cancellationToken));
    }

    private static string Field(ThirdProbeSignPart part) => part == ThirdProbeSignPart.Reflection ? "reflection" : "project";
}
