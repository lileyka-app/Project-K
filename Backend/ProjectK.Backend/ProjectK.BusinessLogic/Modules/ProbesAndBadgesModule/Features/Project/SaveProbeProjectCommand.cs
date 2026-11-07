using MediatR;
using ProjectK.BusinessLogic.Behaviors;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.BusinessLogic.Services.Audit;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Project;

/// <summary>What the project form sends, as the УПЮ template lays a project out.</summary>
public sealed record ProbeProjectInput(
    string? Title,
    ProbeProjectArea? Area,
    IReadOnlyList<ProbeProjectReach>? Reach,
    bool IsShared,
    string? SharedWith,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    string? Location,
    string? EventLeads,
    string? CoordinatorName,
    string? CoordinatorContact,
    string? Description,
    string? Goal,
    string? Plan,
    string? Results);

/// <param name="ProjectKey">The project just saved, so a client can upload into a new one.</param>
public sealed record ProbeProjectSavedResponse(Guid ProjectKey, ThirdProbeResponse Tab);

/// <summary>
/// Adds a project to the third probe (<see cref="ProjectKey"/> null) or rewrites one. A new project
/// takes a main place while there is one, then the spare. Pictures the new texts no longer show are
/// dropped with their blobs.
/// </summary>
public sealed record SaveProbeProjectCommand(
    Guid MemberKey,
    string ProbeId,
    Guid? ProjectKey,
    ProbeProjectInput Input) : IRequest<ServiceResult<ProbeProjectSavedResponse>>, ITransactionalRequest;

public sealed class SaveProbeProjectCommandHandler : IRequestHandler<SaveProbeProjectCommand, ServiceResult<ProbeProjectSavedResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProbeProjectAccess _access;
    private readonly IPhotoService _photos;
    private readonly AuditActorResolver _actors;
    private readonly IKurinAudit _audit;
    private readonly IProbesCatalogService _probes;
    private readonly IThirdProbeReader _reader;

    public SaveProbeProjectCommandHandler(
        IUnitOfWork unitOfWork,
        IProbeProjectAccess access,
        IPhotoService photos,
        AuditActorResolver actors,
        IKurinAudit audit,
        IProbesCatalogService probes,
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

    public async Task<ServiceResult<ProbeProjectSavedResponse>> Handle(SaveProbeProjectCommand request, CancellationToken cancellationToken)
    {
        var probeId = request.ProbeId.Trim();
        if (await ProbeProjectGuard.RefuseAsync<ProbeProjectSavedResponse>(_access, request.MemberKey, probeId, cancellationToken) is { } refused)
        {
            return refused;
        }

        var input = Normalize(request.Input);
        if (Invalid(input) is { } invalid)
        {
            return ProbeProjectGuard.Refuse<ProbeProjectSavedResponse>(invalid.Code, invalid.Message);
        }

        var imageKeys = ProbeProjectMarkdown.ImageKeys(input.Description!, input.Goal!, input.Plan!, input.Results!);
        if (imageKeys is null)
        {
            return ForeignImage();
        }

        if (imageKeys.Distinct().Count() > ProbeProjectRules.MaxImagesPerProject)
        {
            return ProbeProjectGuard.Refuse<ProbeProjectSavedResponse>(
                "TooManyImages", $"A project holds at most {ProbeProjectRules.MaxImagesPerProject} pictures.");
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
                return ProbeProjectGuard.Refuse<ProbeProjectSavedResponse>(
                    "TooManyProjects", $"The probe holds {ProbeProjectRules.MaxMainProjects} projects and {ProbeProjectRules.MaxSpareProjects} spare.");
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
                return new ServiceResult<ProbeProjectSavedResponse>(ResultType.NotFound);
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
            return ProbeProjectGuard.Refuse<ProbeProjectSavedResponse>(
                "TooManyShared", $"At most {ProbeProjectRules.MaxSharedMainProjects} of the main projects may be shared.");
        }

        var actor = await _actors.ResolveAsync(cancellationToken);
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
                isNew ? AuditActions.Probe.ProjectAdded : AuditActions.Probe.ProjectEdited,
                project.ProbeProjectKey,
                ProgressAuditLabels.Probe(_probes, probeId),
                request.MemberKey,
                isNew ? AuditChanges.Added(("project", project.Title)) : AuditChanges.Of(("project", titleBefore, project.Title))),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // After the save, so a failed save never leaves a text pointing at deleted files. A blob
        // missed here is swept by the orphan cleanup: no row names it any more.
        foreach (var image in dropped)
        {
            await _photos.DeletePhotoAsync(image.BlobName, cancellationToken);
        }

        return new ServiceResult<ProbeProjectSavedResponse>(
            isNew ? ResultType.Created : ResultType.Success,
            new ProbeProjectSavedResponse(project.ProbeProjectKey, await _reader.ReadAsync(request.MemberKey, probeId, cancellationToken)));
    }

    private static void Apply(ProbeProject project, ProbeProjectInput input)
    {
        project.Title = input.Title!;
        project.Area = input.Area;
        project.Reach = ProbeProjectMapping.Join(input.Reach);
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
        SharedWith = Optional(input.SharedWith),
        Location = Optional(input.Location),
        EventLeads = Optional(input.EventLeads),
        CoordinatorName = Optional(input.CoordinatorName),
        CoordinatorContact = Optional(input.CoordinatorContact),
        Description = input.Description?.Trim() ?? string.Empty,
        Goal = input.Goal?.Trim() ?? string.Empty,
        Plan = input.Plan?.Trim() ?? string.Empty,
        Results = input.Results?.Trim() ?? string.Empty
    };

    private static ServiceResult<ProbeProjectSavedResponse> ForeignImage() =>
        ProbeProjectGuard.Refuse<ProbeProjectSavedResponse>("ForeignImage", "Pictures must be uploaded into the project.");

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static (string Code, string Message)? Invalid(ProbeProjectInput input)
    {
        const int shortText = ProbeProjectRules.MaxShortTextLength;
        if (input.Title!.Length == 0)
        {
            return ("TitleRequired", "A project needs a title.");
        }

        if (new[] { input.Title, input.SharedWith, input.Location, input.EventLeads, input.CoordinatorName, input.CoordinatorContact }
            .Any(text => text?.Length > shortText))
        {
            return ("FieldTooLong", $"Short fields hold at most {shortText} characters.");
        }

        if (new[] { input.Description, input.Goal, input.Plan, input.Results }.Any(text => text!.Length > ProbeProjectRules.MaxTextLength))
        {
            return ("DescriptionTooLong", $"Each text holds at most {ProbeProjectRules.MaxTextLength} characters.");
        }

        if (input.Area is { } area && !Enum.IsDefined(area))
        {
            return ("UnknownArea", "Unknown project area.");
        }

        if (input.StartsOn is { } start && input.EndsOn is { } end && end < start)
        {
            return ("EndsBeforeStart", "A project cannot end before it starts.");
        }

        return null;
    }
}
