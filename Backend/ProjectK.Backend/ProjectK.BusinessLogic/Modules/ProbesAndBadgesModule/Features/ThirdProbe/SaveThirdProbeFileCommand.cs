using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.ThirdProbe;

/// <summary>
/// What the person writes into their third-probe file. The confirmation of the reflection and the
/// stages are not here: leaders set those.
/// </summary>
public sealed record ThirdProbeFileInput(
    string? MentorZvyazkovyi,
    string? MentorVporiadnyk,
    string? MentorReferent,
    DateOnly? ReflectionOn,
    string? ReflectionWith,
    ThirdProbeCampKind? CampKind,
    string? CampName,
    DateOnly? CampFrom,
    DateOnly? CampTo,
    string? PhysicalBadgeId,
    string? Resume);

/// <summary>
/// Writes the third-probe file. A confirmed reflection whose date or partner changes loses its
/// confirmation: what was confirmed is no longer what is written.
/// </summary>
public sealed record SaveThirdProbeFileCommand(Guid MemberKey, string ProbeId, ThirdProbeFileInput Input)
    : IRequest<ServiceResult<ThirdProbeDto>>;

public sealed class SaveThirdProbeFileCommandHandler : IRequestHandler<SaveThirdProbeFileCommand, ServiceResult<ThirdProbeDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProbesAndBadgesAccess _access;
    private readonly ICurrentMember _actors;
    private readonly IKurinAudit _audit;
    private readonly IProbesCatalogReader _probes;
    private readonly IThirdProbeReader _reader;

    public SaveThirdProbeFileCommandHandler(
        IUnitOfWork unitOfWork,
        IProbesAndBadgesAccess access,
        ICurrentMember actors,
        IKurinAudit audit,
        IProbesCatalogReader probes,
        IThirdProbeReader reader)
    {
        _unitOfWork = unitOfWork;
        _access = access;
        _actors = actors;
        _audit = audit;
        _probes = probes;
        _reader = reader;
    }

    public async Task<ServiceResult<ThirdProbeDto>> Handle(SaveThirdProbeFileCommand request, CancellationToken cancellationToken)
    {
        var probeId = request.ProbeId.Trim();
        if (await ProbeProjectWriteRules.RefuseAsync<ThirdProbeDto>(_access, request.MemberKey, probeId, cancellationToken) is { } refused)
        {
            return refused;
        }

        var input = Normalize(request.Input);
        if (Invalid(input) is { } invalid)
        {
            return ProbeProjectWriteRules.Refuse<ThirdProbeDto>(invalid.Code, invalid.Message);
        }

        if (input.PhysicalBadgeId is { } badgeId && !await HoldsAsync(request.MemberKey, badgeId, cancellationToken))
        {
            return ProbeProjectWriteRules.Refuse<ThirdProbeDto>(ProbesAndBadgesErrors.BadgeNotHeld, "The вмілість must be one the person holds, confirmed.");
        }

        var file = await ThirdProbeFileWriter.GetOrAddAsync(_unitOfWork, request.MemberKey, probeId, cancellationToken);
        if (file.ReflectionOn != input.ReflectionOn || file.ReflectionWith != input.ReflectionWith)
        {
            file.ReflectionConfirmedAtUtc = null;
            file.ReflectionConfirmedByUserKey = null;
            file.ReflectionConfirmedByName = null;
        }

        var actor = await _actors.ActorAsync(cancellationToken);
        file.MentorZvyazkovyi = input.MentorZvyazkovyi;
        file.MentorVporiadnyk = input.MentorVporiadnyk;
        file.MentorReferent = input.MentorReferent;
        file.ReflectionOn = input.ReflectionOn;
        file.ReflectionWith = input.ReflectionWith;
        file.CampKind = input.CampKind;
        file.CampName = input.CampName;
        file.CampFrom = input.CampFrom;
        file.CampTo = input.CampTo;
        file.PhysicalBadgeId = input.PhysicalBadgeId;
        file.Resume = input.Resume!;
        file.UpdatedDate = DateTime.UtcNow;
        file.UpdatedByUserKey = actor.UserKey;
        file.UpdatedByName = actor.Name;

        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Probe,
                AuditActions.Probe.FileEdited,
                file.ThirdProbeFileKey,
                ProgressAuditNames.Probe(_probes, probeId),
                request.MemberKey),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<ThirdProbeDto>(ResultType.Success, await _reader.ReadAsync(request.MemberKey, probeId, cancellationToken));
    }

    private async Task<bool> HoldsAsync(Guid memberKey, string badgeId, CancellationToken cancellationToken)
    {
        var progress = await _unitOfWork.BadgeProgresses.GetByMemberAndBadgeIdAsync(memberKey, badgeId, cancellationToken);
        return progress?.Status == BadgeProgressStatus.Confirmed;
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ThirdProbeFileInput Normalize(ThirdProbeFileInput input) => input with
    {
        MentorZvyazkovyi = Optional(input.MentorZvyazkovyi),
        MentorVporiadnyk = Optional(input.MentorVporiadnyk),
        MentorReferent = Optional(input.MentorReferent),
        ReflectionWith = Optional(input.ReflectionWith),
        CampName = Optional(input.CampName),
        PhysicalBadgeId = Optional(input.PhysicalBadgeId),
        Resume = input.Resume?.Trim() ?? string.Empty
    };

    private static (string Code, string Message)? Invalid(ThirdProbeFileInput input)
    {
        const int shortText = ProbeProjectRules.MaxShortTextLength;
        if (new[] { input.MentorZvyazkovyi, input.MentorVporiadnyk, input.MentorReferent, input.ReflectionWith, input.CampName }
            .Any(text => text?.Length > shortText))
        {
            return (ProbesAndBadgesErrors.FieldTooLong, $"Short fields hold at most {shortText} characters.");
        }

        if (input.Resume!.Length > ProbeProjectRules.MaxResumeLength)
        {
            return (ProbesAndBadgesErrors.DescriptionTooLong, $"The résumé holds at most {ProbeProjectRules.MaxResumeLength} characters.");
        }

        // The résumé carries no pictures: there is nowhere to upload them to.
        if (ProbeProjectMarkdownRules.ImageKeys(input.Resume) is not { Count: 0 })
        {
            return (ProbesAndBadgesErrors.ForeignImage, "The résumé holds text only.");
        }

        if (input.CampKind is { } kind && !Enum.IsDefined(kind))
        {
            return (ProbesAndBadgesErrors.UnknownCampKind, "Unknown camp kind.");
        }

        if (input.CampFrom is { } from && input.CampTo is { } to && to < from)
        {
            return (ProbesAndBadgesErrors.EndsBeforeStart, "A camp cannot end before it starts.");
        }

        return null;
    }
}
