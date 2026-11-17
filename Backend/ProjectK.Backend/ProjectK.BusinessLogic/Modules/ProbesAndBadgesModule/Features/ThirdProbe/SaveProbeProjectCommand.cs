using MediatR;
using ProjectK.BusinessLogic.Behaviors;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.ProbesAndBadgesModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.ThirdProbe;

/// <summary>
/// Adds a project to the third probe (<see cref="ProjectKey"/> null) or rewrites one. A new project
/// takes a main place while there is one, then the spare. Pictures the new texts no longer show are
/// dropped with their blobs.
/// </summary>
public sealed record SaveProbeProjectCommand(
    Guid MemberKey,
    string ProbeId,
    Guid? ProjectKey,
    ProbeProjectInput Input) : IRequest<ServiceResult<ProbeProjectSavedDto>>, ITransactionalRequest;

public sealed class SaveProbeProjectCommandHandler : IRequestHandler<SaveProbeProjectCommand, ServiceResult<ProbeProjectSavedDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProbesAndBadgesAccess _access;
    private readonly IPhotoService _photos;
    private readonly ICurrentMember _actors;
    private readonly IKurinAudit _audit;
    private readonly IProbesCatalogReader _probes;
    private readonly IThirdProbeReader _reader;

    public SaveProbeProjectCommandHandler(
        IUnitOfWork unitOfWork,
        IProbesAndBadgesAccess access,
        IPhotoService photos,
        ICurrentMember actors,
        IKurinAudit audit,
        IProbesCatalogReader probes,
        IThirdProbeReader reader)
    {
        _unitOfWork = unitOfWork;
        _access = access;
        _photos = photos;
        _actors = actors;
        _audit = audit;
        _probes = probes;
        _reader = reader;
    }

    public async Task<ServiceResult<ProbeProjectSavedDto>> Handle(SaveProbeProjectCommand request, CancellationToken cancellationToken)
    {
        var probeId = request.ProbeId.Trim();
        if (await ProbeProjectWriteRules.RefuseAsync<ProbeProjectSavedDto>(_access, request.MemberKey, probeId, cancellationToken) is { } refused)
        {
            return refused;
        }

        var input = Normalize(request.Input);
        if (Invalid(input) is { } invalid)
        {
            return ProbeProjectWriteRules.Refuse<ProbeProjectSavedDto>(invalid.Code, invalid.Message);
        }

        var imageKeys = ProbeProjectMarkdownRules.ImageKeys(input.Description!, input.Goal!, input.Plan!, input.Results!);
        if (imageKeys is null)
        {
            return ForeignImage();
        }

        if (imageKeys.Distinct().Count() > ProbeProjectRules.MaxImagesPerProject)
        {
            return ProbeProjectWriteRules.Refuse<ProbeProjectSavedDto>(
                ProbesAndBadgesErrors.TooManyImages, $"A project holds at most {ProbeProjectRules.MaxImagesPerProject} pictures.");
        }

        await _unitOfWork.ProbeProjects.LockAsync(request.MemberKey, probeId, cancellationToken);
        var all = await _unitOfWork.ProbeProjects.GetForMemberProbeAsync(request.MemberKey, probeId, cancellationToken);
        ProbeProject project;
        string? titleBefore = null;
        var isNew = request.ProjectKey is null;
        if (isNew)
        {
            var mains = all.Count(p => !p.IsSpare);
            var spares = all.Count(p => p.IsSpare);
            if (mains >= ProbeProjectRules.MaxMainProjects && spares >= ProbeProjectRules.MaxSpareProjects)
            {
                return ProbeProjectWriteRules.Refuse<ProbeProjectSavedDto>(
                    ProbesAndBadgesErrors.TooManyProjects, $"The probe holds {ProbeProjectRules.MaxMainProjects} projects and {ProbeProjectRules.MaxSpareProjects} spare.");
            }

            // A new project has no pictures yet: they are uploaded into a project that exists.
            if (imageKeys.Count > 0)
            {
                return ForeignImage();
            }

            project = new ProbeProject
            {
                MemberKey = request.MemberKey,
                ProbeId = probeId,
                IsSpare = mains >= ProbeProjectRules.MaxMainProjects
            };
            _unitOfWork.ProbeProjects.Create(project, cancellationToken);
        }
        else
        {
            var found = all.FirstOrDefault(p => p.ProbeProjectKey == request.ProjectKey);
            if (found is null)
            {
                return ServiceResult<ProbeProjectSavedDto>.Failure(ResultType.NotFound, ProbesAndBadgesErrors.ProjectNotFound, "No such project in this probe.");
            }

            project = found;
            titleBefore = project.Title;
            var own = project.Images.Select(image => image.ProbeProjectImageKey).ToHashSet();
            if (imageKeys.Any(key => !own.Contains(key)))
            {
                return ForeignImage();
            }
        }

        if (input.IsShared && !project.IsSpare
            && all.Count(p => p != project && p.IsShared && !p.IsSpare) >= ProbeProjectRules.MaxSharedMainProjects)
        {
            return ProbeProjectWriteRules.Refuse<ProbeProjectSavedDto>(
                ProbesAndBadgesErrors.TooManyShared, $"At most {ProbeProjectRules.MaxSharedMainProjects} of the main projects may be shared.");
        }

        var actor = await _actors.ActorAsync(cancellationToken);
        Apply(project, input);
        project.UpdatedDate = DateTime.UtcNow;
        project.UpdatedByUserKey = actor.UserKey;
        project.UpdatedByName = actor.Name;

        var shown = imageKeys.ToHashSet();
        var dropped = project.Images.Where(image => !shown.Contains(image.ProbeProjectImageKey)).ToList();
        _unitOfWork.ProbeProjects.RemoveImages(dropped);
        foreach (var image in dropped)
        {
            project.Images.Remove(image);
        }

        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Probe,
                isNew ? AuditActionNames.Probe.ProjectAdded : AuditActionNames.Probe.ProjectEdited,
                project.ProbeProjectKey,
                ProgressAuditNames.Probe(_probes, probeId),
                request.MemberKey,
                isNew ? AuditChangeRules.Added(("project", project.Title)) : AuditChangeRules.Of(("project", titleBefore, project.Title))),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // After the save, so a failed save never leaves a text pointing at deleted files. A blob
        // missed here is swept by the orphan cleanup: no row names it any more.
        foreach (var image in dropped)
        {
            await _photos.DeletePhotoAsync(image.BlobName, cancellationToken);
        }

        return new ServiceResult<ProbeProjectSavedDto>(
            isNew ? ResultType.Created : ResultType.Success,
            new ProbeProjectSavedDto(project.ProbeProjectKey, await _reader.ReadAsync(request.MemberKey, probeId, cancellationToken)));
    }

    private static void Apply(ProbeProject project, ProbeProjectInput input)
    {
        project.Title = input.Title!;
        project.Area = input.Area;
        project.Reach = ThirdProbeMapping.Join(input.Reach);
        project.IsShared = input.IsShared;
        project.SharedWith = input.IsShared ? input.SharedWith : null;
        project.StartsOn = input.StartsOn;
        project.EndsOn = input.EndsOn;
        project.Location = input.Location;
        project.EventLeads = input.EventLeads;
        project.CoordinatorName = input.CoordinatorName;
        project.CoordinatorContact = input.CoordinatorContact;
        project.Description = input.Description!;
        project.Goal = input.Goal!;
        project.Plan = input.Plan!;
        project.Results = input.Results!;
    }

    private static ProbeProjectInput Normalize(ProbeProjectInput input) => input with
    {
        Title = input.Title?.Trim() ?? string.Empty,
        SharedWith = TextRules.TrimOrNull(input.SharedWith),
        Location = TextRules.TrimOrNull(input.Location),
        EventLeads = TextRules.TrimOrNull(input.EventLeads),
        CoordinatorName = TextRules.TrimOrNull(input.CoordinatorName),
        CoordinatorContact = TextRules.TrimOrNull(input.CoordinatorContact),
        Description = input.Description?.Trim() ?? string.Empty,
        Goal = input.Goal?.Trim() ?? string.Empty,
        Plan = input.Plan?.Trim() ?? string.Empty,
        Results = input.Results?.Trim() ?? string.Empty
    };

    private static ServiceResult<ProbeProjectSavedDto> ForeignImage() =>
        ProbeProjectWriteRules.Refuse<ProbeProjectSavedDto>(ProbesAndBadgesErrors.ForeignImage, "Pictures must be uploaded into the project.");

    private static (string Code, string Message)? Invalid(ProbeProjectInput input)
    {
        const int shortText = ProbeProjectRules.MaxShortTextLength;
        if (input.Title!.Length == 0)
        {
            return (ProbesAndBadgesErrors.TitleRequired, "A project needs a title.");
        }

        if (new[] { input.Title, input.SharedWith, input.Location, input.EventLeads, input.CoordinatorName, input.CoordinatorContact }
            .Any(text => text?.Length > shortText))
        {
            return (ProbesAndBadgesErrors.FieldTooLong, $"Short fields hold at most {shortText} characters.");
        }

        if (new[] { input.Description, input.Goal, input.Plan, input.Results }.Any(text => text!.Length > ProbeProjectRules.MaxTextLength))
        {
            return (ProbesAndBadgesErrors.DescriptionTooLong, $"Each text holds at most {ProbeProjectRules.MaxTextLength} characters.");
        }

        if (input.Area is { } area && !Enum.IsDefined(area))
        {
            return (ProbesAndBadgesErrors.UnknownArea, "Unknown project area.");
        }

        if (input.StartsOn is { } start && input.EndsOn is { } end && end < start)
        {
            return (ProbesAndBadgesErrors.EndsBeforeStart, "A project cannot end before it starts.");
        }

        return null;
    }
}
