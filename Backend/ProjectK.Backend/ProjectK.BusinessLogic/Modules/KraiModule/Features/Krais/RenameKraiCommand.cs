using MediatR;
using ProjectK.BusinessLogic.Modules.KraiModule.Services;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KraiModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Features.Krais;

/// <summary>Renames a край. Its code stays: kurins, profiles and the catalogue point at it.</summary>
public sealed record RenameKraiCommand(string Code, string Name) : IRequest<ServiceResult<KraiDto>>;

public sealed class RenameKraiCommandHandler : IRequestHandler<RenameKraiCommand, ServiceResult<KraiDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackendCache _cache;
    private readonly ICatalogKraiNames _catalogNames;

    public RenameKraiCommandHandler(IUnitOfWork unitOfWork, IBackendCache cache, ICatalogKraiNames catalogNames)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _catalogNames = catalogNames;
    }

    public async Task<ServiceResult<KraiDto>> Handle(RenameKraiCommand request, CancellationToken cancellationToken)
    {
        if (KraiNames.Clean(request.Name) is not { } name)
        {
            return KraiNames.Invalid();
        }

        var code = request.Code?.Trim().ToLowerInvariant() ?? string.Empty;
        var krai = await _unitOfWork.Krais.GetForUpdateAsync(code, cancellationToken);
        if (krai is null)
        {
            return ServiceResult<KraiDto>.Failure(ResultType.NotFound, KraiErrors.KraiNotFound, "Krai not found.");
        }

        if (krai.Name != name && await KraiNames.TakenAsync(_unitOfWork, name, except: krai.Code, cancellationToken))
        {
            return KraiNames.Taken(name);
        }

        if (krai.Name != name)
        {
            krai.Name = name;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _cache.Invalidate(BackendCachePolicies.KraiProfileReads);
            _catalogNames.Name(krai.Code, name);
        }

        var kurins = await _unitOfWork.Kurins.GetAllAsync(cancellationToken);
        return new ServiceResult<KraiDto>(ResultType.Success, krai.ToDto(kurins.Count(k => k.KraiCode == krai.Code)));
    }
}
