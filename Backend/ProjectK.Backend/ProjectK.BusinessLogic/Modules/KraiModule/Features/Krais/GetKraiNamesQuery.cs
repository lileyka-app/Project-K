using MediatR;
using Microsoft.Extensions.Options;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Models.Dtos.KraiModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Features.Krais;

/// <summary>
/// Only the codes and names of the краї, for the public waitlist form to offer: nothing about the
/// kurins in them, since anyone may read it.
/// </summary>
public sealed record GetKraiNamesQuery : IRequest<ServiceResult<IReadOnlyList<KraiNameDto>>>;

public sealed class GetKraiNamesQueryHandler : IRequestHandler<GetKraiNamesQuery, ServiceResult<IReadOnlyList<KraiNameDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly KraiOptions _krai;

    public GetKraiNamesQueryHandler(IUnitOfWork unitOfWork, IOptions<KraiOptions> krai)
    {
        _unitOfWork = unitOfWork;
        _krai = krai.Value;
    }

    public async Task<ServiceResult<IReadOnlyList<KraiNameDto>>> Handle(GetKraiNamesQuery request, CancellationToken cancellationToken)
    {
        var krais = await _unitOfWork.Krais.GetAllAsync(cancellationToken);

        IReadOnlyList<KraiNameDto> response = krais
            .Select(krai => krai.ToNameDto(_krai.Default))
            .ToList();

        return new ServiceResult<IReadOnlyList<KraiNameDto>>(ResultType.Success, response);
    }
}
