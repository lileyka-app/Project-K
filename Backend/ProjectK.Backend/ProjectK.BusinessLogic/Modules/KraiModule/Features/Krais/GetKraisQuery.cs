using MediatR;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KraiModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Features.Krais;

/// <summary>The краї this instance knows, with how many kurins each holds.</summary>
public sealed record GetKraisQuery : IRequest<ServiceResult<IReadOnlyList<KraiDto>>>;

public sealed class GetKraisQueryHandler : IRequestHandler<GetKraisQuery, ServiceResult<IReadOnlyList<KraiDto>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetKraisQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<IReadOnlyList<KraiDto>>> Handle(GetKraisQuery request, CancellationToken cancellationToken)
    {
        var krais = await _unitOfWork.Krais.GetAllAsync(cancellationToken);
        var kurins = await _unitOfWork.Kurins.GetAllAsync(cancellationToken);
        var counts = kurins.GroupBy(k => k.KraiCode).ToDictionary(g => g.Key, g => g.Count());

        IReadOnlyList<KraiDto> response = krais
            .Select(krai => krai.ToDto(counts.GetValueOrDefault(krai.Code)))
            .ToList();

        return new ServiceResult<IReadOnlyList<KraiDto>>(ResultType.Success, response);
    }
}
