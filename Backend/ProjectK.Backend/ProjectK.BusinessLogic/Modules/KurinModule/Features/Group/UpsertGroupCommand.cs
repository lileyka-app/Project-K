using MediatR;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Group;

public class UpsertGroupCommand : IRequest<ServiceResult<GroupDto>>
{
    public Guid GroupKey { get; set; }
    public string Name { get; set; }
    public Guid KurinKey { get; set; }
    public string? Description { get; set; }

    public UpsertGroupCommand(Guid groupKey, string name, string? description = null)
    {
        GroupKey = groupKey;
        Name = name;
        Description = description;
    }

    public UpsertGroupCommand(string name, Guid kurinKey, string? description = null)
    {
        KurinKey = kurinKey;
        Name = name;
        Description = description;
    }
}

public class UpsertGroupCommandHandler : IRequestHandler<UpsertGroupCommand, ServiceResult<GroupDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobReadLinks _links;
    private readonly IBackendCache _cache;
    public UpsertGroupCommandHandler(IUnitOfWork unitOfWork, IBlobReadLinks links, IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _links = links;
        _cache = cache;
    }

    public async Task<ServiceResult<GroupDto>> Handle(UpsertGroupCommand request, CancellationToken cancellationToken)
    {
        var description = GroupMapping.Trimmed(request.Description);
        if (description?.Length > 1000)
        {
            return ServiceResult<GroupDto>.Failure(ResultType.BadRequest, KurinErrors.DescriptionTooLong, "Description must be 1000 characters or fewer.");
        }

        var existing = await _unitOfWork.Groups.GetByKeyAsync(request.GroupKey, cancellationToken);
        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        bool isCreated = false;

        if (existing == null)
        {
            if (kurin == null)
            {
                return KurinErrors.NotFound<GroupDto>();
            }

            // A senior kurin has no гуртки at all: its people are told apart by tags.
            if (KurinBranchSet.IsSenior(kurin.Branch))
            {
                return ServiceResult<GroupDto>.Failure(ResultType.BadRequest, KurinErrors.NoGroupsInSeniorKurin, "A УСП or УПС kurin has no гуртки.");
            }

            // Create new Group
            existing = new(request.Name, request.KurinKey, description);
            _unitOfWork.Groups.Create(existing, cancellationToken);
            isCreated = true;
        }
        else
        {
            // Update existing Group
            request.ApplyTo(existing, DateTime.UtcNow);
            _unitOfWork.Groups.Update(existing, cancellationToken);
        }

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (changes <= 0)
        {
            return ServiceResult<GroupDto>.Failure(ResultType.InternalServerError, KurinErrors.GroupSaveFailed, "The group was not saved.");
        }

        var response = existing.ToDto(_links);
        _cache.Invalidate(BackendCachePolicies.GroupReads);

        return isCreated
            ? new ServiceResult<GroupDto>(
                ResultType.Created,
                response,
                CreatedAtActionName: "GetByKey",
                CreatedAtRouteValues: new { groupKey = response.GroupKey })
            : new ServiceResult<GroupDto>(ResultType.Success, response);
    }
}
