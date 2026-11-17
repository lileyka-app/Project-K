using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Group;

public sealed class DeleteGroupSilhouetteCommand : IRequest<ServiceResult<GroupDto>>
{
    public Guid GroupKey { get; }

    public DeleteGroupSilhouetteCommand(Guid groupKey)
    {
        GroupKey = groupKey;
    }
}

public sealed class DeleteGroupSilhouetteCommandHandler : IRequestHandler<DeleteGroupSilhouetteCommand, ServiceResult<GroupDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPhotoService _photoService;
    private readonly IBlobReadLinks _links;
    private readonly IBackendCache _cache;

    public DeleteGroupSilhouetteCommandHandler(
        IUnitOfWork unitOfWork,
        IPhotoService photoService,
        IBlobReadLinks links,
        IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _photoService = photoService;
        _links = links;
        _cache = cache;
    }

    public async Task<ServiceResult<GroupDto>> Handle(DeleteGroupSilhouetteCommand request, CancellationToken cancellationToken)
    {
        var group = await _unitOfWork.Groups.GetByKeyAsync(request.GroupKey, cancellationToken);
        if (group == null)
        {
            return KurinErrors.GroupMissing<GroupDto>();
        }

        var oldBlobName = group.SilhouetteBlobName;
        if (string.IsNullOrWhiteSpace(oldBlobName))
        {
            return new ServiceResult<GroupDto>(ResultType.Success, group.ToDto(_links));
        }

        group.SilhouetteBlobName = null;
        _unitOfWork.Groups.Update(group, cancellationToken);

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (changes <= 0)
        {
            return ServiceResult<GroupDto>.Failure(ResultType.InternalServerError, KurinErrors.GroupSaveFailed, "The group was not saved.");
        }

        await _photoService.DeletePhotoAsync(oldBlobName, cancellationToken);
        _cache.Invalidate(BackendCacheRules.GroupReads);

        return new ServiceResult<GroupDto>(ResultType.Success, group.ToDto(_links));
    }
}
