using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.ThirdProbe;

/// <summary>The referent's stages, each the day it happened; null where it has not.</summary>
public sealed record ThirdProbeStagesInput(
    DateOnly? MotivationLetterOn,
    DateOnly? SelfReflectionOn,
    DateOnly? ApplicationSubmittedOn,
    DateOnly? OpenedOn,
    DateOnly? CompletedOn,
    DateOnly? NamedOn,
    DateOnly? ArchivedOn);

/// <summary>
/// Sets the stages of a person's third probe, as the referent of the third probe tracks them —
/// from Lilejka or from the referent's own tool through the same route. Taken whole: a stage left
/// null is cleared. Setting them is the probe's signing right.
/// </summary>
public sealed record SetThirdProbeStagesCommand(Guid MemberKey, string ProbeId, ThirdProbeStagesInput Stages)
    : IRequest<ServiceResult<ThirdProbeDto>>;

public sealed class SetThirdProbeStagesCommandHandler : IRequestHandler<SetThirdProbeStagesCommand, ServiceResult<ThirdProbeDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProbesAndBadgesAccess _access;
    private readonly ICurrentMember _actors;
    private readonly IKurinAudit _audit;
    private readonly IProbesCatalogReader _probes;
    private readonly IThirdProbeReader _reader;
    private readonly ThirdProbeFileWriter _files;

    public SetThirdProbeStagesCommandHandler(
        IUnitOfWork unitOfWork,
        IProbesAndBadgesAccess access,
        ICurrentMember actors,
        IKurinAudit audit,
        IProbesCatalogReader probes,
        IThirdProbeReader reader,
        ThirdProbeFileWriter files)
    {
        _unitOfWork = unitOfWork;
        _access = access;
        _actors = actors;
        _audit = audit;
        _probes = probes;
        _reader = reader;
        _files = files;
    }

    public async Task<ServiceResult<ThirdProbeDto>> Handle(SetThirdProbeStagesCommand request, CancellationToken cancellationToken)
    {
        var probeId = request.ProbeId.Trim();
        var memberKey = request.MemberKey;
        if (await ProbeProjectWriteRules.RefuseAsync<ThirdProbeDto>(
                _access, memberKey, probeId, cancellationToken, access => access.CanSignAsync(memberKey, cancellationToken))
            is { } refused)
        {
            return refused;
        }

        var stages = request.Stages;
        if (OutOfOrder(stages) is { } message)
        {
            return ProbeProjectWriteRules.Refuse<ThirdProbeDto>(ProbesAndBadgesErrors.StagesOutOfOrder, message);
        }

        var file = await _files.GetOrAddAsync(memberKey, probeId, cancellationToken);
        var before = ThirdProbeReader.Stages(file);
        var actor = await _actors.ActorAsync(cancellationToken);
        file.MotivationLetterOn = stages.MotivationLetterOn;
        file.SelfReflectionOn = stages.SelfReflectionOn;
        file.ApplicationSubmittedOn = stages.ApplicationSubmittedOn;
        file.OpenedOn = stages.OpenedOn;
        file.CompletedOn = stages.CompletedOn;
        file.NamedOn = stages.NamedOn;
        file.ArchivedOn = stages.ArchivedOn;
        file.StagesUpdatedAtUtc = DateTime.UtcNow;
        file.StagesUpdatedByName = actor.Name;

        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Probe,
                AuditActions.Probe.StagesChanged,
                file.ThirdProbeFileKey,
                ProgressAuditNames.Probe(_probes, probeId),
                memberKey,
                AuditChanges.Of(
                    ("motivationLetter", before.MotivationLetterOn, stages.MotivationLetterOn),
                    ("selfReflection", before.SelfReflectionOn, stages.SelfReflectionOn),
                    ("applicationSubmitted", before.ApplicationSubmittedOn, stages.ApplicationSubmittedOn),
                    ("opened", before.OpenedOn, stages.OpenedOn),
                    ("completed", before.CompletedOn, stages.CompletedOn),
                    ("named", before.NamedOn, stages.NamedOn),
                    ("archived", before.ArchivedOn, stages.ArchivedOn))),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<ThirdProbeDto>(ResultType.Success, await _reader.ReadAsync(memberKey, probeId, cancellationToken));
    }

    /// <summary>
    /// Closing comes after opening, and naming after closing. The opening steps are not ordered
    /// among themselves (a 16-year-old writes no motivation letter at all), and the archive may come
    /// at any time: a person who leaves or ages out is archived without closing the probe.
    /// </summary>
    private static string? OutOfOrder(ThirdProbeStagesInput stages)
    {
        if (stages.CompletedOn is { } completed && stages.OpenedOn is { } opened && completed < opened)
        {
            return "The probe cannot be done before it was opened.";
        }

        if (stages.NamedOn is { } named && (stages.CompletedOn is not { } done || named < done))
        {
            return "Naming comes after the probe is done.";
        }

        return null;
    }
}
