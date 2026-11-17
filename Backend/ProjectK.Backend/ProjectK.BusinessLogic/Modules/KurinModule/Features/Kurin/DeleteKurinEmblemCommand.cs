using MediatR;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;

/// <summary>Takes the emblem off; the kurin is shown with its plain placeholder again.</summary>
public sealed record DeleteKurinEmblemCommand(Guid KurinKey) : IRequest<ServiceResult<KurinDto>>;

public sealed class DeleteKurinEmblemCommandHandler : IRequestHandler<DeleteKurinEmblemCommand, ServiceResult<KurinDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPhotoService _photoService;
    private readonly IBlobReadLinks _links;
    private readonly IBackendCache _cache;
    private readonly IMembershipDirectory _memberships;

    public DeleteKurinEmblemCommandHandler(IUnitOfWork unitOfWork, IPhotoService photoService, IBlobReadLinks links, IBackendCache cache, IMembershipDirectory memberships)
    {
        _unitOfWork = unitOfWork;
        _photoService = photoService;
        _links = links;
        _cache = cache;
        _memberships = memberships;
    }

    public async Task<ServiceResult<KurinDto>> Handle(DeleteKurinEmblemCommand request, CancellationToken cancellationToken)
    {
        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return KurinErrors.NotFound<KurinDto>();
        }

        var oldBlobName = kurin.EmblemBlobName;
        if (string.IsNullOrWhiteSpace(oldBlobName))
        {
            return new ServiceResult<KurinDto>(ResultType.Success, kurin.ToDto(_links, await _memberships.CountCurrentInKurinAsync(kurin.KurinKey, cancellationToken)));
        }

        kurin.EmblemBlobName = null;
        _unitOfWork.Kurins.Update(kurin, cancellationToken);
        if (await _unitOfWork.SaveChangesAsync(cancellationToken) <= 0)
        {
            return ServiceResult<KurinDto>.Failure(ResultType.InternalServerError, KurinErrors.KurinSaveFailed, "The kurin was not saved.");
        }

        await _photoService.DeletePhotoAsync(oldBlobName, cancellationToken);
        _cache.Invalidate(BackendCacheRules.KurinReads);
        return new ServiceResult<KurinDto>(ResultType.Success, kurin.ToDto(_links, await _memberships.CountCurrentInKurinAsync(kurin.KurinKey, cancellationToken)));
    }
}
