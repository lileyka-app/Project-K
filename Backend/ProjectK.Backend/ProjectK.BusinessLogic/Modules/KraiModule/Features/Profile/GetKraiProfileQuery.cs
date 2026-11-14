using MediatR;
using ProjectK.BusinessLogic.Modules.KraiModule.Services;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Models.Dtos.KraiModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Features.Profile;

/// <summary>The край's profile as the admin edits it.</summary>
public sealed record GetKraiProfileQuery(string KraiCode) : IRequest<ServiceResult<KraiProfileEditorDto>>;

public sealed class GetKraiProfileQueryHandler : IRequestHandler<GetKraiProfileQuery, ServiceResult<KraiProfileEditorDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetKraiProfileQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<KraiProfileEditorDto>> Handle(GetKraiProfileQuery request, CancellationToken cancellationToken)
    {
        var code = request.KraiCode.Trim().ToLowerInvariant();
        var krai = await _unitOfWork.Krais.GetAsync(code, cancellationToken);
        if (krai is null)
        {
            return KraiErrors.NotFound<KraiProfileEditorDto>();
        }

        var revision = await _unitOfWork.Krais.GetLatestProfileRevisionAsync(code, cancellationToken);
        // A revision that does not parse is shown empty: saving over it is how it gets fixed.
        var document = KraiProfileEditor.TryParse(revision?.Json);
        return new ServiceResult<KraiProfileEditorDto>(ResultType.Success, krai.ToEditorDto(revision?.Revision ?? 0, document));
    }
}
