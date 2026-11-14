using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;

/// <summary>Takes the emblem off; the kurin is shown with its plain placeholder again.</summary>
public sealed record DeleteKurinEmblemCommand(Guid KurinKey) : IRequest<ServiceResult<KurinDto>>;

public sealed class DeleteKurinEmblemCommandHandler : IRequestHandler<DeleteKurinEmblemCommand, ServiceResult<KurinDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPhotoService _photoService;
    private readonly IBlobReadLinks _links;
    private readonly IBackendCache _cache;

    public DeleteKurinEmblemCommandHandler(IUnitOfWork unitOfWork, IPhotoService photoService, IBlobReadLinks links, IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _photoService = photoService;
        _links = links;
        _cache = cache;
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
            return new ServiceResult<KurinDto>(ResultType.Success, kurin.ToDto(_links));
        }

        kurin.EmblemBlobName = null;
        _unitOfWork.Kurins.Update(kurin, cancellationToken);
        if (await _unitOfWork.SaveChangesAsync(cancellationToken) <= 0)
        {
            return ServiceResult<KurinDto>.Failure(ResultType.InternalServerError, KurinErrors.KurinSaveFailed, "The kurin was not saved.");
        }

        await _photoService.DeletePhotoAsync(oldBlobName, cancellationToken);
        _cache.Invalidate(BackendCachePolicies.KurinReads);
        return new ServiceResult<KurinDto>(ResultType.Success, kurin.ToDto(_links));
    }
}
