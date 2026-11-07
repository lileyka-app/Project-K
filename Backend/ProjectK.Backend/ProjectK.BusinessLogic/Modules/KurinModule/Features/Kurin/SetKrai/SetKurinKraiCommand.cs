using AutoMapper;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.SetKrai;

/// <summary>Moves a kurin to another край. Admin-only: the край decides what the kurin sees.</summary>
public sealed record SetKurinKraiCommand(Guid KurinKey, string KraiCode) : IRequest<ServiceResult<KurinResponse>>;

public sealed class SetKurinKraiCommandHandler : IRequestHandler<SetKurinKraiCommand, ServiceResult<KurinResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IBackendCache _cache;

    public SetKurinKraiCommandHandler(IUnitOfWork unitOfWork, IMapper mapper, IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<ServiceResult<KurinResponse>> Handle(SetKurinKraiCommand request, CancellationToken cancellationToken)
    {
        var kraiCode = request.KraiCode?.Trim().ToLowerInvariant() ?? string.Empty;

        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return ServiceResult<KurinResponse>.Failure(ResultType.NotFound, "KurinNotFound", "Kurin not found.");
        }

        if (kurin.KraiCode == kraiCode)
        {
            return new ServiceResult<KurinResponse>(ResultType.Success, _mapper.Map<KurinResponse>(kurin));
        }

        if (!await _unitOfWork.Krais.ExistsAsync(kraiCode, cancellationToken))
        {
            return ServiceResult<KurinResponse>.Failure(ResultType.BadRequest, "UnknownKrai", $"There is no край '{kraiCode}'.");
        }

        // The numbers travel with the kurin, so each has to be free in its гілка of the край it moves to.
        foreach (var numbering in kurin.Numbering)
        {
            if (numbering.Number is int number
                && await _unitOfWork.Kurins.IsNumberTakenAsync(kraiCode, numbering.Branch, number, kurin.KurinKey, cancellationToken))
            {
                return ServiceResult<KurinResponse>.Failure(ResultType.Conflict, "KurinNumberTaken", "A kurin with this number already exists in that край.");
            }
        }

        kurin.KraiCode = kraiCode;
        _unitOfWork.Kurins.Update(kurin, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _cache.Invalidate(BackendCachePolicies.KurinReads);
        _cache.Invalidate(BackendCachePolicies.GroupReads);

        return new ServiceResult<KurinResponse>(ResultType.Success, _mapper.Map<KurinResponse>(kurin));
    }
}
