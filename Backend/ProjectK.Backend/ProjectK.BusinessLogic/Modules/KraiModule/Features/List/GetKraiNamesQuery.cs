using MediatR;
using Microsoft.Extensions.Options;
using ProjectK.BusinessLogic.Modules.KraiModule.Models;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Features.List;

/// <summary>
/// Only the codes and names of the краї, for the public waitlist form to offer: nothing about the
/// kurins in them, since anyone may read it.
/// </summary>
public sealed record GetKraiNamesQuery : IRequest<ServiceResult<IReadOnlyList<KraiNameResponse>>>;

public sealed class GetKraiNamesQueryHandler : IRequestHandler<GetKraiNamesQuery, ServiceResult<IReadOnlyList<KraiNameResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly KraiOptions _krai;

    public GetKraiNamesQueryHandler(IUnitOfWork unitOfWork, IOptions<KraiOptions> krai)
    {
        _unitOfWork = unitOfWork;
        _krai = krai.Value;
    }

    public async Task<ServiceResult<IReadOnlyList<KraiNameResponse>>> Handle(GetKraiNamesQuery request, CancellationToken cancellationToken)
    {
        var krais = await _unitOfWork.Krais.GetAllAsync(cancellationToken);

        IReadOnlyList<KraiNameResponse> response = krais
            .Select(krai => new KraiNameResponse
            {
                Code = krai.Code,
                Name = krai.Name,
                IsDefault = krai.Code == _krai.Default
            })
            .ToList();

        return new ServiceResult<IReadOnlyList<KraiNameResponse>>(ResultType.Success, response);
    }
}
