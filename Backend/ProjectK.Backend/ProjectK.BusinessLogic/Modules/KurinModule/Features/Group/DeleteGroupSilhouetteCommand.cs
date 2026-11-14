using AutoMapper;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Group;

public sealed class DeleteGroupSilhouetteCommand : IRequest<ServiceResult<GroupResponse>>
{
    public Guid GroupKey { get; }

    public DeleteGroupSilhouetteCommand(Guid groupKey)
    {
        GroupKey = groupKey;
    }
}

public sealed class DeleteGroupSilhouetteCommandHandler : IRequestHandler<DeleteGroupSilhouetteCommand, ServiceResult<GroupResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPhotoService _photoService;
    private readonly IMapper _mapper;
    private readonly IBackendCache _cache;

    public DeleteGroupSilhouetteCommandHandler(
        IUnitOfWork unitOfWork,
        IPhotoService photoService,
        IMapper mapper,
        IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _photoService = photoService;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<ServiceResult<GroupResponse>> Handle(DeleteGroupSilhouetteCommand request, CancellationToken cancellationToken)
    {
        var group = await _unitOfWork.Groups.GetByKeyAsync(request.GroupKey, cancellationToken);
        if (group == null)
        {
            return new ServiceResult<GroupResponse>(ResultType.NotFound);
        }

        var oldBlobName = group.SilhouetteBlobName;
        if (string.IsNullOrWhiteSpace(oldBlobName))
        {
            return new ServiceResult<GroupResponse>(ResultType.Success, _mapper.Map<GroupResponse>(group));
        }

        group.SilhouetteBlobName = null;
        _unitOfWork.Groups.Update(group, cancellationToken);

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (changes <= 0)
        {
            return new ServiceResult<GroupResponse>(ResultType.InternalServerError);
        }

        await _photoService.DeletePhotoAsync(oldBlobName, cancellationToken);
        _cache.Invalidate(BackendCachePolicies.GroupReads);

        return new ServiceResult<GroupResponse>(ResultType.Success, _mapper.Map<GroupResponse>(group));
    }
}
