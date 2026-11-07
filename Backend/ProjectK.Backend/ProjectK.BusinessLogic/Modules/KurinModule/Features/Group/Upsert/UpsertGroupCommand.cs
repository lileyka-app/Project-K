using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.Upsert;

public class UpsertGroupCommand : IRequest<ServiceResult<GroupResponse>>
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

public class UpsertGroupCommandHandler : IRequestHandler<UpsertGroupCommand, ServiceResult<GroupResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IBackendCache _cache;
    public UpsertGroupCommandHandler(IUnitOfWork unitOfWork, IMapper mapper, IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<ServiceResult<GroupResponse>> Handle(UpsertGroupCommand request, CancellationToken cancellationToken)
    {
        var description = NormalizeOptionalText(request.Description);
        if (description?.Length > 1000)
        {
            return ServiceResult<GroupResponse>.Failure(ResultType.BadRequest, "DescriptionTooLong", "Description must be 1000 characters or fewer.");
        }

        var existing = await _unitOfWork.Groups.GetByKeyAsync(request.GroupKey, cancellationToken);
        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        bool isCreated = false;

        if (existing == null)
        {
            if (kurin == null)
            {
                return new ServiceResult<GroupResponse>(ResultType.NotFound);
            }

            // A senior kurin has no гуртки at all: its people are told apart by tags.
            if (KurinBranchSet.IsSenior(kurin.Branch))
            {
                return ServiceResult<GroupResponse>.Failure(ResultType.BadRequest, "NoGroupsInSeniorKurin", "A УСП or УПС kurin has no гуртки.");
            }

            // Create new Group
            existing = new(request.Name, request.KurinKey, description);
            _unitOfWork.Groups.Create(existing, cancellationToken);
            isCreated = true;
        }
        else
        {
            // Update existing Group
            _mapper.Map(request, existing);
            existing.Description = description;
            _unitOfWork.Groups.Update(existing, cancellationToken);
        }

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (changes <= 0)
        {
            return new ServiceResult<GroupResponse>(
                ResultType.InternalServerError);
        }

        var response = _mapper.Map<GroupResponse>(existing);
        _cache.Invalidate(BackendCachePolicies.GroupReads);

        return isCreated
            ? new ServiceResult<GroupResponse>(
                ResultType.Created,
                response,
                CreatedAtActionName: "GetByKey",
                CreatedAtRouteValues: new { groupKey = response.GroupKey })
            : new ServiceResult<GroupResponse>(ResultType.Success, response);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
