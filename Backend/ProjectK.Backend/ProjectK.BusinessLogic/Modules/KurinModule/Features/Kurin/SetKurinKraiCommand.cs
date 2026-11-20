using MediatR;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;

/// <summary>Moves a kurin to another край. Admin-only: the край decides what the kurin sees.</summary>
public sealed record SetKurinKraiCommand(Guid KurinKey, string KraiCode) : IRequest<ServiceResult<KurinDto>>;

public sealed class SetKurinKraiCommandHandler : IRequestHandler<SetKurinKraiCommand, ServiceResult<KurinDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobReadLinks _links;
    private readonly IBackendCache _cache;
    private readonly IMembershipDirectory _memberships;

    public SetKurinKraiCommandHandler(IUnitOfWork unitOfWork, IBlobReadLinks links, IBackendCache cache, IMembershipDirectory memberships)
    {
        _unitOfWork = unitOfWork;
        _links = links;
        _cache = cache;
        _memberships = memberships;
    }

    public async Task<ServiceResult<KurinDto>> Handle(SetKurinKraiCommand request, CancellationToken cancellationToken)
    {
        var kraiCode = request.KraiCode?.Trim().ToLowerInvariant() ?? string.Empty;

        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return KurinErrors.NotFound<KurinDto>();
        }

        if (kurin.KraiCode == kraiCode)
        {
            return new ServiceResult<KurinDto>(ResultType.Success, kurin.ToDto(_links, await _memberships.CountCurrentInKurinAsync(kurin.KurinKey, cancellationToken)));
        }

        if (!await _unitOfWork.Krais.ExistsAsync(kraiCode, cancellationToken))
        {
            return ServiceResult<KurinDto>.Failure(ResultType.BadRequest, KurinErrors.UnknownKrai, $"There is no край '{kraiCode}'.");
        }

        // The numbers travel with the kurin, so each has to be free in its гілка of the край it moves to.
        foreach (var numbering in kurin.Numbering)
        {
            if (numbering.Number is int number
                && await _unitOfWork.Kurins.IsNumberTakenAsync(kraiCode, numbering.Branch, number, kurin.KurinKey, cancellationToken))
            {
                return ServiceResult<KurinDto>.Failure(ResultType.Conflict, KurinErrors.KurinNumberTaken, "A kurin with this number already exists in that край.");
            }
        }

        kurin.KraiCode = kraiCode;
        _unitOfWork.Kurins.Update(kurin, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _cache.Invalidate(BackendCacheRules.KurinReads);
        _cache.Invalidate(BackendCacheRules.GroupReads);

        return new ServiceResult<KurinDto>(ResultType.Success, kurin.ToDto(_links, await _memberships.CountCurrentInKurinAsync(kurin.KurinKey, cancellationToken)));
    }
}
