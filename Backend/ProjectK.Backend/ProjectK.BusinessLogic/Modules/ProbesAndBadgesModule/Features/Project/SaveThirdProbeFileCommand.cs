using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.BusinessLogic.Services.Audit;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Project;

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
    : IRequest<ServiceResult<ThirdProbeResponse>>;

public sealed class SaveThirdProbeFileCommandHandler : IRequestHandler<SaveThirdProbeFileCommand, ServiceResult<ThirdProbeResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProbeProjectAccess _access;
    private readonly AuditActorResolver _actors;
    private readonly IKurinAudit _audit;
    private readonly IProbesCatalogService _probes;
    private readonly IThirdProbeReader _reader;

    public SaveThirdProbeFileCommandHandler(
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

    public async Task<ServiceResult<ThirdProbeResponse>> Handle(SaveThirdProbeFileCommand request, CancellationToken cancellationToken)
    {
        var probeId = request.ProbeId.Trim();
        if (await ProbeProjectGuard.RefuseAsync<ThirdProbeResponse>(_access, request.MemberKey, probeId, cancellationToken) is { } refused)
        {
            return refused;
        }

        var input = Normalize(request.Input);
        if (Invalid(input) is { } invalid)
        {
            return ProbeProjectGuard.Refuse<ThirdProbeResponse>(invalid.Code, invalid.Message);
        }

        if (input.PhysicalBadgeId is { } badgeId && !await HoldsAsync(request.MemberKey, badgeId, cancellationToken))
        {
            return ProbeProjectGuard.Refuse<ThirdProbeResponse>("BadgeNotHeld", "The вмілість must be one the person holds, confirmed.");
        }

        var file = await ThirdProbeFiles.GetOrAddAsync(_unitOfWork, request.MemberKey, probeId, cancellationToken);
        if (file.ReflectionOn != input.ReflectionOn || file.ReflectionWith != input.ReflectionWith)
        {
            file.ReflectionConfirmedAtUtc = null;
            file.ReflectionConfirmedByUserKey = null;
            file.ReflectionConfirmedByName = null;
        }

        var actor = await _actors.ResolveAsync(cancellationToken);
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
                ProgressAuditLabels.Probe(_probes, probeId),
                request.MemberKey),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<ThirdProbeResponse>(ResultType.Success, await _reader.ReadAsync(request.MemberKey, probeId, cancellationToken));
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
            return ("FieldTooLong", $"Short fields hold at most {shortText} characters.");
        }

        if (input.Resume!.Length > ProbeProjectRules.MaxResumeLength)
        {
            return ("DescriptionTooLong", $"The résumé holds at most {ProbeProjectRules.MaxResumeLength} characters.");
        }

        // The résumé carries no pictures: there is nowhere to upload them to.
        if (ProbeProjectMarkdown.ImageKeys(input.Resume) is not { Count: 0 })
        {
            return ("ForeignImage", "The résumé holds text only.");
        }

        if (input.CampKind is { } kind && !Enum.IsDefined(kind))
        {
            return ("UnknownCampKind", "Unknown camp kind.");
        }

        if (input.CampFrom is { } from && input.CampTo is { } to && to < from)
        {
            return ("EndsBeforeStart", "A camp cannot end before it starts.");
        }

        return null;
    }
}

/// <summary>The one place a third-probe file comes into being: on whichever write needs it first.</summary>
internal static class ThirdProbeFiles
{
    public static async Task<ThirdProbeFile> GetOrAddAsync(IUnitOfWork unitOfWork, Guid memberKey, string probeId, CancellationToken cancellationToken)
    {
        if (await unitOfWork.ProbeProjects.GetFileAsync(memberKey, probeId, cancellationToken) is { } file)
        {
            return file;
        }

        file = new ThirdProbeFile { MemberKey = memberKey, ProbeId = probeId };
        unitOfWork.ProbeProjects.AddFile(file);
        return file;
    }
}
